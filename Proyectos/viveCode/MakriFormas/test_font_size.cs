using System;
using System.Collections.Generic;
using System.IO;
using MakriFormas.Models;
using MakriFormas.Services;

class TestFontSize
{
    static void Main()
    {
        Console.WriteLine("Probando generacion de PDF...");
        
        var items = new List<ProformaItem> 
        { 
            new ProformaItem 
            { 
                Cantidad = 2, 
                Description = "Producto de prueba",
                UnitPrice = 50.0
            } 
        };
        
        // Test 1
        var fileA = "test_pdf_a.pdf";
        if (File.Exists(fileA)) File.Delete(fileA);
        
        Console.WriteLine("Generando primer PDF...");
        PdfGenerator.GenerateProformaPdf(
            fileA,
            "Cliente Test",
            "12345678901",
            DateTime.Now,
            items,
            100.0);
        
        Console.WriteLine($"Archivo generado: {fileA}, Tamaño: {new FileInfo(fileA).Length} bytes");
        
        // Test 2
        var fileB = "test_pdf_b.pdf";
        if (File.Exists(fileB)) File.Delete(fileB);
        
        Console.WriteLine("Generando segundo PDF...");
        PdfGenerator.GenerateProformaPdf(
            fileB,
            "Cliente Test",
            "12345678901",
            DateTime.Now,
            items,
            100.0);
        
        Console.WriteLine($"Archivo generado: {fileB}, Tamaño: {new FileInfo(fileB).Length} bytes");
        
        // Comparar tamaños
        var sizeA = new FileInfo(fileA).Length;
        var sizeB = new FileInfo(fileB).Length;
        
        Console.WriteLine($"\nComparación:");
        Console.WriteLine($"- Tamaño A: {sizeA} bytes");
        Console.WriteLine($"- Tamaño B: {sizeB} bytes");
        Console.WriteLine($"- Diferencia: {sizeB - sizeA} bytes");
        
        // Limpiar
        File.Delete(fileA);
        File.Delete(fileB);
        
        Console.WriteLine("\nPrueba completada.");
    }
}