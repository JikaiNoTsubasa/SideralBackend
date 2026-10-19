using System.Text;

namespace sideral_jobcode.Users;

/// <summary>
/// Identifiant automatique sur 3 lettres majuscules : 1re lettre du prénom + 2 premières du nom (Jean Dupont → JDU).
/// S'il est déjà pris, on prend les lettres suivantes du nom pour la 3e lettre (JDP, JDO, JDN, JDT), puis d'autres
/// paires de lettres du nom (JUP, JUO...), puis la 1re lettre du nom suivie de A à Z.
/// Accents retirés, seules les lettres A-Z sont gardées ("Le Gall-Hervé" → LEGALLHERVE).
/// </summary>
public static class LoginGenerator
{
    public const int LENGTH = 3;

    public static IEnumerable<string> Candidates(string firstname, string lastname)
    {
        var first = Normalize(firstname);
        var last = Normalize(lastname);
        if (first.Length == 0 || last.Length == 0)
        {
            yield break;
        }

        var f = first[0];
        var seen = new HashSet<string>();
        IEnumerable<string> All()
        {
            for (var k = 1; k < last.Length; k++)
                yield return $"{f}{last[0]}{last[k]}";
            for (var i = 1; i < last.Length; i++)
                for (var j = i + 1; j < last.Length; j++)
                    yield return $"{f}{last[i]}{last[j]}";
            for (var c = 'A'; c <= 'Z'; c++)
                yield return $"{f}{last[0]}{c}";
        }

        foreach (var candidate in All())
        {
            if (seen.Add(candidate))
                yield return candidate;
        }
    }

    /// <summary>Majuscules sans accents, lettres A-Z uniquement (la décomposition sépare les accents des lettres).</summary>
    public static string Normalize(string? value)
    {
        var decomposed = (value ?? string.Empty)
            .ToUpperInvariant()
            .Replace("Œ", "OE").Replace("Æ", "AE").Replace("ß", "SS").Replace("ẞ", "SS")
            .Normalize(NormalizationForm.FormD);
        return new string(decomposed.Where(c => c is >= 'A' and <= 'Z').ToArray());
    }
}
