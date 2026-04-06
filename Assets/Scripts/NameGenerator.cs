using UnityEngine;

// NameGenerator produces a random adjective + noun display name (e.g. "SwiftFalcon").
// It is a static utility class — no MonoBehaviour, no scene dependency.
// The word lists are curated to sound game-appropriate and pair well together.
public static class NameGenerator
{
    private static readonly string[] Adjectives =
    {
        "Swift", "Brave", "Bold", "Fierce", "Calm",
        "Sly", "Iron", "Stone", "Frost", "Shadow",
        "Silent", "Wild", "Amber", "Crimson", "Golden",
        "Silver", "Rusty", "Grim", "Keen", "Dark",
        "Lucky", "Stout", "Noble", "Rogue", "Stormy",
    };

    private static readonly string[] Nouns =
    {
        "Falcon", "Wolf", "Bear", "Fox", "Hawk",
        "Raven", "Tiger", "Drake", "Arrow", "Blade",
        "Shield", "Lance", "Forge", "Ember", "Thorn",
        "Stone", "Fang", "Crest", "Dusk", "Salt",
        "Reef", "Ridge", "Bolt", "Sage", "Pyre",
    };

    // Returns a PascalCase name such as "BoldRaven" or "FrostBlade".
    // Uses Unity's built-in Random so it works in both Editor and builds.
    public static string Generate()
    {
        string adjective = Adjectives[Random.Range(0, Adjectives.Length)];
        string noun = Nouns[Random.Range(0, Nouns.Length)];
        return adjective + noun;
    }
}
