namespace sideral_context.Users;

/// <summary>Valeurs autorisées pour les préférences d'affichage (le rendu est géré par le frontend).</summary>
public static class UserPreferences
{
    public const string THEME_LIGHT = "light";
    public const string THEME_DARK = "dark";
    public const string THEME_SYSTEM = "system";
    public static readonly string[] THEMES = [THEME_LIGHT, THEME_DARK, THEME_SYSTEM];

    public const string DEFAULT_ACCENT = "indigo";
    public static readonly string[] ACCENT_COLORS = [DEFAULT_ACCENT, "blue", "teal", "green", "orange", "rose", "violet"];
}
