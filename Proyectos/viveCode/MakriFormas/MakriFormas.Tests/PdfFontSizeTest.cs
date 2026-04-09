using System;
using System.Collections.Generic;
using System.IO;
using MakriFormas.Models;
using MakriFormas.Services;
using Xunit;

namespace MakriFormas.Tests
{
    public class PdfFontSizeTest
    {
        [Fact]
        public void GeneratePdf_WithDifferentData_ShouldCreateFiles()
        {
            // Arrange
            var items = new List<ProformaItem> 
            { 
                new ProformaItem 
                { 
                    Cantidad = 2, 
                    Description = "Producto de prueba",
                    UnitPrice = 50.0
                } 
            };
            
            var testFileA = "test_data_a.pdf";
            var testFileB = "test_data_b.pdf";
            
            // Clean up any existing test files
            if (File.Exists(testFileA)) File.Delete(testFileA);
            if (File.Exists(testFileB)) File.Delete(testFileB);
            
            // Act - Generate first PDF
            PdfGenerator.GenerateProformaPdf(
                testFileA,
                "Cliente Test",
                "12345678901",
                DateTime.Now,
                items,
                100.0);
            
            // Act - Generate second PDF
            PdfGenerator.GenerateProformaPdf(
                testFileB,
                "Cliente Test 2",
                "12345678901",
                DateTime.Now,
                items,
                100.0);
            
            // Assert - Both files should be created
            Assert.True(File.Exists(testFileA), $"File {testFileA} should exist");
            Assert.True(File.Exists(testFileB), $"File {testFileB} should exist");
            
            var sizeA = new FileInfo(testFileA).Length;
            var sizeB = new FileInfo(testFileB).Length;
            
            Assert.True(sizeA > 0, "First file should have content");
            Assert.True(sizeB > 0, "Second file should have content");
            
            Console.WriteLine($"File size A: {sizeA} bytes");
            Console.WriteLine($"File size B: {sizeB} bytes");
            
            // Clean up
            File.Delete(testFileA);
            File.Delete(testFileB);
        }
        
        [Fact]
        public void GeneratePdf_ShouldUseDefaultLayout()
        {
            // Arrange
            var items = new List<ProformaItem> 
            { 
                new ProformaItem 
                { 
                    Cantidad = 1, 
                    Description = "Test",
                    UnitPrice = 100.0
                } 
            };
            
            var testFileDefault = "test_default_font.pdf";
            if (File.Exists(testFileDefault)) File.Delete(testFileDefault);
            
            // Act
            PdfGenerator.GenerateProformaPdf(
                testFileDefault,
                "Cliente",
                "123",
                DateTime.Now,
                items,
                100.0);
            
            // Assert
            Assert.True(File.Exists(testFileDefault));
            
            // Clean up
            File.Delete(testFileDefault);
        }
    }
}