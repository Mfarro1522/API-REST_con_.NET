using System;
using System.Collections.Generic;
using System.IO;
using MakriFormas.Models;
using MakriFormas.Services;
using Xunit;

namespace MakriFormas.Tests
{
    public class PdfFontTest
    {
        [Fact]
        public void GeneratePdf_MultipleCalls_ShouldSucceed()
        {
            var items = new List<ProformaItem> { new ProformaItem { Cantidad = 1, Description = "Test", UnitPrice = 100 } };
            PdfGenerator.GenerateProformaPdf("test_10.pdf", "Cliente 1", "123", DateTime.Now, items, 100);
            PdfGenerator.GenerateProformaPdf("test_24.pdf", "Cliente 2", "123", DateTime.Now, items, 100);

            var size10 = new FileInfo("test_10.pdf").Length;
            var size24 = new FileInfo("test_24.pdf").Length;
            
            // They should be different or generated successfully.
            Assert.True(size10 > 0);
            Assert.True(size24 > 0);
        }
    }
}
