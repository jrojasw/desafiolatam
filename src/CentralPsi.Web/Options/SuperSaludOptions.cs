namespace CentralPsi.Web.Options;

public class SuperSaludOptions
{
    public const string SectionName = "SuperSalud";

    public string ValidationBaseUrl { get; set; } = "https://emisorcertificados.superdesalud.gob.cl/ValidacionCertificados/";
}
