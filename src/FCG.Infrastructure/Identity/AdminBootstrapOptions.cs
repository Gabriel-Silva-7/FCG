namespace FCG.Infrastructure.Identity;

public sealed class AdminBootstrapOptions
{
    public const string SectionName = "AdminBootstrap";

    // Valores padrão de seed, usados apenas em Development — o hosted service nem executa em
    // outro ambiente. Existem porque o enunciado pede que os usuários de teste venham prontos e
    // documentados no README: quem avalia precisa conseguir logar sem configurar nada.
    // Sobrescreva por user-secrets se quiser outras credenciais.
    public string? Email { get; set; } = "admin@fcg.local";

    public string? Password { get; set; } = "Admin@123456";

    public string? PlayerEmail { get; set; } = "player@fcg.local";

    public string? PlayerPassword { get; set; } = "Player@123456";
}
