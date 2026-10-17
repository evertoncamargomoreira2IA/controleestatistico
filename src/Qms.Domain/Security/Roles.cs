namespace Qms.Domain.Security;

public static class Roles
{
    public const string Admin = "admin";
    public const string Engenheiro = "engenheiro";
    public const string Tecnico = "tecnico";
    public const string Qualidade = "qualidade";
    public const string Leitor = "leitor";

    public static IReadOnlyList<string> All { get; } =
    [Admin, Engenheiro, Tecnico, Qualidade, Leitor];
}
