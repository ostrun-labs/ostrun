using System.Text;

namespace OstrunGenerator;

public static class Naming
{
    // "Jwt__ExpirationMinutes" -> "JWT_EXPIRATION_MINUTES" (ASP.NET Core's
    // "__" section separator becomes "_", each PascalCase segment becomes
    // its own SCREAMING_SNAKE word). Deterministic and collision-free is
    // worth more here than matching any one-off hand-written naming choice.
    public static string ToScreamingSnake(string name)
    {
        var segments = name.Split("__", StringSplitOptions.RemoveEmptyEntries);
        return string.Join('_', segments.Select(PascalSegmentToSnake));
    }

    private static string PascalSegmentToSnake(string segment)
    {
        var sb = new StringBuilder();
        for (var i = 0; i < segment.Length; i++)
        {
            var c = segment[i];
            if (i > 0 && char.IsUpper(c) && char.IsLower(segment[i - 1]))
            {
                sb.Append('_');
            }
            sb.Append(char.ToUpperInvariant(c));
        }
        return sb.ToString();
    }

    // "auth" -> "AUTH_", used to prefix every env var this service owns in
    // the app-level .env.example, so two services never collide.
    public static string EnvPrefix(string serviceKey) => serviceKey.ToUpperInvariant() + "_";
}
