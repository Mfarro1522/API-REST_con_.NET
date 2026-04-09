using System;
using System.Collections.Generic;
using MakriFormas.Models;
using MakriFormas.Services;

class Program {
    static void Main() {
        var items = new List<ProformaItem> { new ProformaItem { Cantidad = 1, Description = "Test Item", Total = 100 } };
        PdfGenerator.GenerateProformaPdf("test_10.pdf", "Test Client", "12345", DateTime.Now, items, 100);
        PdfGenerator.GenerateProformaPdf("test_24.pdf", "Test Client", "12345", DateTime.Now, items, 100);
        Console.WriteLine("Done.");
    }
}
