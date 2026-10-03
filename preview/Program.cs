using System.Reflection;

internal static class Preview
{
    [STAThread]
    static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        var type = Assembly.Load("SteamFixVN").GetType("SteamFixVN.MainForm", throwOnError: true)!;
        using var form = (Form)Activator.CreateInstance(type, new object[] { null! })!;
        form.Show();
        Application.DoEvents();
        using var image = new Bitmap(form.Width, form.Height);
        form.DrawToBitmap(image, new Rectangle(Point.Empty, form.Size));
        image.Save(args[0], System.Drawing.Imaging.ImageFormat.Png);
        Console.WriteLine($"Rendered UI: {form.Text}, {form.Width} x {form.Height}. No Apply invoked.");
        form.Close();
    }
}
