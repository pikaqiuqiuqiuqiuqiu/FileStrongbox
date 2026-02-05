namespace FileStrongbox.Models;

public class AppSettings
{
    public FileNameFormat FileNameFormat { get; set; } = FileNameFormat.FullEncrypt;
    public string CustomExtension { get; set; } = ".data";
}
