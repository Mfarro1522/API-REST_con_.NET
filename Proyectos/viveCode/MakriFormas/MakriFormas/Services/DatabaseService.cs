using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using MakriFormas.Models;
using Microsoft.Data.Sqlite;

namespace MakriFormas.Services
{
    public static class DatabaseService
    {
        private const string DatabaseFileName = "makriformas.db";

        public static string DatabaseDirectory => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MakriFormas");

        public static string DatabasePath => Path.Combine(DatabaseDirectory, DatabaseFileName);

        public static void Initialize()
        {
            Directory.CreateDirectory(DatabaseDirectory);

            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Products (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Sku TEXT NOT NULL,
                    Name TEXT NOT NULL,
                    Category TEXT NOT NULL,
                    StockQuantity REAL NOT NULL DEFAULT 0,
                    Unit TEXT NOT NULL DEFAULT 'und',
                    UnitPrice REAL NOT NULL
                );

                CREATE TABLE IF NOT EXISTS Proformas (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Code TEXT NOT NULL UNIQUE,
                    IssueDate TEXT NOT NULL,
                    DeliveryDate TEXT,
                    ClientName TEXT NOT NULL,
                    Ruc TEXT,
                    Address TEXT,
                    DesignerObservations TEXT,
                    InstallerObservations TEXT,
                    ItemCount INTEGER NOT NULL,
                    Total REAL NOT NULL,
                    ItemsJson TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL,
                    UpdatedAt TEXT NOT NULL
                );";
            command.ExecuteNonQuery();
        }

        public static List<Product> GetProducts()
        {
            Initialize();

            var products = new List<Product>();
            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Sku, Name, Category, StockQuantity, Unit, UnitPrice
                FROM Products
                ORDER BY Name;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                products.Add(new Product
                {
                    Id = reader.GetInt32(0),
                    Sku = reader.GetString(1),
                    Name = reader.GetString(2),
                    Category = reader.GetString(3),
                    StockQuantity = reader.GetDouble(4),
                    Unit = reader.GetString(5),
                    UnitPrice = reader.GetDouble(6)
                });
            }

            return products;
        }

        public static string GetNextProformaCode()
        {
            Initialize();

            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            return GetNextProformaCode(connection);
        }

        public static int SaveProforma(ProformaDraft draft)
        {
            Initialize();

            var cleanItems = (draft.Items ?? new List<ProformaItem>())
                .Where(i => i.Cantidad > 0 && !string.IsNullOrWhiteSpace(i.Description))
                .ToList();

            if (cleanItems.Count == 0)
            {
                throw new InvalidOperationException("Debes agregar al menos un item válido para guardar la proforma.");
            }

            var issueDate = draft.IssueDate == default ? DateTime.Today : draft.IssueDate.Date;
            var nowIso = DateTime.Now.ToString("O", CultureInfo.InvariantCulture);

            var snapshot = cleanItems.Select(i => new
            {
                i.Description,
                i.Unidad,
                i.Ancho,
                i.Alto,
                i.Longitud,
                i.Cantidad,
                i.UnitPrice,
                Total = i.Total
            });

            var itemsJson = JsonSerializer.Serialize(snapshot);

            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            var code = string.IsNullOrWhiteSpace(draft.Code)
                ? GetNextProformaCode(connection)
                : draft.Code.Trim();

            if (draft.Id.HasValue)
            {
                using var update = connection.CreateCommand();
                update.CommandText = @"
                    UPDATE Proformas
                    SET Code = $code,
                        IssueDate = $issueDate,
                        DeliveryDate = $deliveryDate,
                        ClientName = $clientName,
                        Ruc = $ruc,
                        Address = $address,
                        DesignerObservations = $designer,
                        InstallerObservations = $installer,
                        ItemCount = $itemCount,
                        Total = $total,
                        ItemsJson = $itemsJson,
                        UpdatedAt = $updatedAt
                    WHERE Id = $id;";

                update.Parameters.AddWithValue("$id", draft.Id.Value);
                BindProformaParameters(update, code, issueDate, draft.DeliveryDate, draft.ClientName, draft.Ruc, draft.Address, draft.DesignerObservations, draft.InstallerObservations, cleanItems.Count, cleanItems.Sum(i => i.Total), itemsJson, nowIso);

                var affected = update.ExecuteNonQuery();
                if (affected > 0)
                {
                    return draft.Id.Value;
                }
            }

            using var insert = connection.CreateCommand();
            insert.CommandText = @"
                INSERT INTO Proformas (
                    Code, IssueDate, DeliveryDate, ClientName, Ruc, Address,
                    DesignerObservations, InstallerObservations,
                    ItemCount, Total, ItemsJson, CreatedAt, UpdatedAt)
                VALUES (
                    $code, $issueDate, $deliveryDate, $clientName, $ruc, $address,
                    $designer, $installer,
                    $itemCount, $total, $itemsJson, $createdAt, $updatedAt);
                SELECT last_insert_rowid();";

            BindProformaParameters(insert, code, issueDate, draft.DeliveryDate, draft.ClientName, draft.Ruc, draft.Address, draft.DesignerObservations, draft.InstallerObservations, cleanItems.Count, cleanItems.Sum(i => i.Total), itemsJson, nowIso);
            insert.Parameters.AddWithValue("$createdAt", nowIso);

            var newId = insert.ExecuteScalar();
            return Convert.ToInt32(newId, CultureInfo.InvariantCulture);
        }

        public static List<ProformaHistoryEntry> GetProformaHistory()
        {
            Initialize();

            var rows = new List<ProformaHistoryEntry>();
            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Code, IssueDate, DeliveryDate, ClientName, Ruc, ItemCount, Total
                FROM Proformas
                ORDER BY IssueDate DESC, Id DESC;";

            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var issueDateRaw = reader.GetString(2);
                var deliveryDateRaw = reader.IsDBNull(3) ? null : reader.GetString(3);

                DateTime.TryParse(issueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var issueDate);
                DateTime? deliveryDate = null;
                if (!string.IsNullOrWhiteSpace(deliveryDateRaw) &&
                    DateTime.TryParse(deliveryDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDeliveryDate))
                {
                    deliveryDate = parsedDeliveryDate;
                }

                rows.Add(new ProformaHistoryEntry
                {
                    Id = reader.GetInt32(0),
                    Code = reader.GetString(1),
                    IssueDate = issueDate == default ? DateTime.Today : issueDate,
                    DeliveryDate = deliveryDate,
                    ClientName = reader.GetString(4),
                    Ruc = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                    ItemCount = reader.GetInt32(6),
                    Total = reader.GetDouble(7)
                });
            }

            return rows;
        }

        public static ProformaDraft? GetProformaById(int id)
        {
            Initialize();

            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                SELECT Id, Code, IssueDate, DeliveryDate, ClientName, Ruc, Address,
                       DesignerObservations, InstallerObservations, ItemsJson
                FROM Proformas
                WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", id);

            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return null;
            }

            var issueDateRaw = reader.GetString(2);
            var deliveryDateRaw = reader.IsDBNull(3) ? null : reader.GetString(3);

            DateTime.TryParse(issueDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var issueDate);
            DateTime? deliveryDate = null;
            if (!string.IsNullOrWhiteSpace(deliveryDateRaw) &&
                DateTime.TryParse(deliveryDateRaw, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsedDeliveryDate))
            {
                deliveryDate = parsedDeliveryDate;
            }

            var itemsJson = reader.IsDBNull(9) ? "[]" : reader.GetString(9);
            var items = DeserializeProformaItems(itemsJson);

            return new ProformaDraft
            {
                Id = reader.GetInt32(0),
                Code = reader.GetString(1),
                IssueDate = issueDate == default ? DateTime.Today : issueDate,
                DeliveryDate = deliveryDate,
                ClientName = reader.GetString(4),
                Ruc = reader.IsDBNull(5) ? string.Empty : reader.GetString(5),
                Address = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                DesignerObservations = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                InstallerObservations = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                Items = items
            };
        }

        private static List<ProformaItem> DeserializeProformaItems(string json)
        {
            var items = new List<ProformaItem>();

            try
            {
                using var doc = JsonDocument.Parse(json);
                foreach (var element in doc.RootElement.EnumerateArray())
                {
                    var item = new ProformaItem
                    {
                        Description = element.TryGetProperty("Description", out var desc) ? desc.GetString() ?? string.Empty : string.Empty,
                        UnitPrice = element.TryGetProperty("UnitPrice", out var p) ? p.GetDouble() : 0
                    };

                    // ── Nuevo formato con Unidad ──────────────────────────────────
                    if (element.TryGetProperty("Unidad", out var unidadProp))
                    {
                        item.Unidad   = unidadProp.GetString() ?? "unidad";
                        item.Ancho    = element.TryGetProperty("Ancho",    out var aw) ? aw.GetDouble() : 1;
                        item.Alto     = element.TryGetProperty("Alto",     out var ah) ? ah.GetDouble() : 1;
                        item.Longitud = element.TryGetProperty("Longitud", out var al) ? al.GetDouble() : 1;
                        item.Cantidad = element.TryGetProperty("Cantidad", out var ac) ? ac.GetDouble() : 1;
                    }
                    else
                    {
                        // ── Migración del formato viejo (IsAreaBased + Quantity) ──
                        var isAreaBased = element.TryGetProperty("IsAreaBased", out var area) && area.GetBoolean();
                        item.Unidad   = isAreaBased ? "m2" : "unidad";
                        item.Ancho    = element.TryGetProperty("Width",  out var w) ? w.GetDouble() : 1;
                        item.Alto     = element.TryGetProperty("Height", out var h) ? h.GetDouble() : 1;
                        item.Longitud = 1;

                        // Cantidad puede estar serializada como int o double
                        if (element.TryGetProperty("Cantidad", out var cDbl))
                            item.Cantidad = cDbl.GetDouble();
                        else if (element.TryGetProperty("Quantity", out var qInt))
                            item.Cantidad = qInt.TryGetDouble(out var qd) ? qd : qInt.GetInt32();
                        else
                            item.Cantidad = 1;
                    }

                    items.Add(item);
                }
            }
            catch
            {
                // If JSON is malformed, return empty list
            }

            return items;
        }

        public static void AddProduct(Product product)
        {
            Initialize();

            using var connection = CreateConnection(DatabasePath);
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT INTO Products (Sku, Name, Category, StockQuantity, Unit, UnitPrice)
                VALUES ($sku, $name, $category, $stockQuantity, $unit, $unitPrice);";

            command.Parameters.AddWithValue("$sku", product.Sku.Trim());
            command.Parameters.AddWithValue("$name", product.Name.Trim());
            command.Parameters.AddWithValue("$category", product.Category.Trim());
            command.Parameters.AddWithValue("$stockQuantity", product.StockQuantity);
            command.Parameters.AddWithValue("$unit", product.Unit.Trim());
            command.Parameters.AddWithValue("$unitPrice", product.UnitPrice);

            command.ExecuteNonQuery();
        }

        public static void ExportDatabase(string destinationPath)
        {
            Initialize();
            File.Copy(DatabasePath, destinationPath, overwrite: true);
        }

        public static void ImportDatabase(string sourcePath)
        {
            ValidateSQLiteFile(sourcePath);

            Directory.CreateDirectory(DatabaseDirectory);
            File.Copy(sourcePath, DatabasePath, overwrite: true);

            // Ensure the imported database has the required schema.
            Initialize();
        }

        private static SqliteConnection CreateConnection(string databasePath)
        {
            return new SqliteConnection($"Data Source={databasePath};Mode=ReadWriteCreate");
        }

        private static void BindProformaParameters(
            SqliteCommand command,
            string code,
            DateTime issueDate,
            DateTime? deliveryDate,
            string clientName,
            string ruc,
            string address,
            string designerObservations,
            string installerObservations,
            int itemCount,
            double total,
            string itemsJson,
            string nowIso)
        {
            command.Parameters.AddWithValue("$code", code);
            command.Parameters.AddWithValue("$issueDate", issueDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            command.Parameters.AddWithValue("$deliveryDate", deliveryDate?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);
            command.Parameters.AddWithValue("$clientName", (clientName ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$ruc", (ruc ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$address", (address ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$designer", (designerObservations ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$installer", (installerObservations ?? string.Empty).Trim());
            command.Parameters.AddWithValue("$itemCount", itemCount);
            command.Parameters.AddWithValue("$total", total);
            command.Parameters.AddWithValue("$itemsJson", itemsJson);
            command.Parameters.AddWithValue("$updatedAt", nowIso);
        }

        private static string GetNextProformaCode(SqliteConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT Code FROM Proformas ORDER BY Id DESC LIMIT 1;";
            var lastCodeObj = command.ExecuteScalar();

            var currentYear = DateTime.Now.Year;
            if (lastCodeObj is not string lastCode || string.IsNullOrWhiteSpace(lastCode))
            {
                return $"PF-{currentYear}-0001";
            }

            var chunks = lastCode.Split('-');
            if (chunks.Length == 3 &&
                int.TryParse(chunks[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var lastYear) &&
                int.TryParse(chunks[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sequence))
            {
                if (lastYear == currentYear)
                {
                    return $"PF-{currentYear}-{(sequence + 1):0000}";
                }
            }

            return $"PF-{currentYear}-0001";
        }

        private static void SeedProductsIfNeeded(SqliteConnection connection)
        {
            // Dummy data generation removed as requested.
        }

        private static void SeedProformasIfNeeded(SqliteConnection connection)
        {
            // Dummy data generation removed as requested.
        }

        public static void DeleteProduct(int id)
        {
            using var connection = CreateConnection(DatabasePath);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Products WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public static void DeleteProforma(int id)
        {
            using var connection = CreateConnection(DatabasePath);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "DELETE FROM Proformas WHERE Id = $id";
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        public static void UpdateProductPrice(int id, double newPrice)
        {
            using var connection = CreateConnection(DatabasePath);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandText = "UPDATE Products SET UnitPrice = $price WHERE Id = $id";
            command.Parameters.AddWithValue("$price", newPrice);
            command.Parameters.AddWithValue("$id", id);
            command.ExecuteNonQuery();
        }

        private static void ValidateSQLiteFile(string sourcePath)
        {
            using var connection = new SqliteConnection($"Data Source={sourcePath};Mode=ReadOnly");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' LIMIT 1;";
            command.ExecuteScalar();
        }
    }
}