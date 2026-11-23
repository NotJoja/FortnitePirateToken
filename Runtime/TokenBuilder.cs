using System.Collections.Generic;
using System.Linq;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FortnitePirateToken.Runtime;

/// <summary>
/// Builds the Pirate Shop Token valuable prefab at runtime from a vanilla valuable (which provides the networking and
/// physics components) and registers it as a network prefab. It is deliberately NOT added to any level preset: the
/// spawn patch rolls for it separately.
/// </summary>
internal static class TokenBuilder
{
    public const string Name = "Pirate Shop Token";
    private const string Folder = "token";

    // The model is ~0.19 m wide; scaled up a little so it is easy to grab.
    private const float Scale = 1.5f;

    // The mesh origin is not the centre: the model spans y -0.0305..0.0140.
    private const float ModelCentreY = -0.0083f;

    public static PrefabRef? Prefab { get; private set; }

    private static GameObject? _holder;

    private static Transform Holder
    {
        get
        {
            if (_holder == null)
            {
                _holder = new GameObject("FortnitePirateToken Prefabs");
                _holder.SetActive(false);
                Object.DontDestroyOnLoad(_holder);
            }

            return _holder.transform;
        }
    }

    /// <returns>true when finished (registered or failed), false when the vanilla template is not loadable yet.</returns>
    public static bool TryRegister()
    {
        var template = FindTemplate();
        if (template == null) return false;

        try
        {
            var prefab = Build(template);
            Prefab = NetworkPrefabs.RegisterNetworkPrefab($"Valuables/{Name}", prefab);
            if (Prefab == null)
            {
                FortnitePirateTokenPlugin.Logger.LogError("REPOLib refused to register the token prefab.");
            }
            else
            {
                FortnitePirateTokenPlugin.Logger.LogInfo($"Registered '{Name}' (template '{template.name}').");
            }
        }
        catch (System.Exception e)
        {
            FortnitePirateTokenPlugin.Logger.LogError($"Failed to build '{Name}': {e}");
        }

        return true;
    }

    private static GameObject? FindTemplate()
    {
        var exact = Resources.Load<GameObject>("Valuables/01 Tiny/Valuable Diamond");
        if (exact != null) return exact;

        return Resources.LoadAll<GameObject>("Valuables/01 Tiny").FirstOrDefault(IsPlainValuable)
               ?? Resources.FindObjectsOfTypeAll<ValuableObject>()
                   .Where(v => !v.gameObject.scene.IsValid() && v.GetComponent<Rigidbody>() != null)
                   .Select(v => v.gameObject)
                   .FirstOrDefault();
    }

    private static bool IsPlainValuable(GameObject go) =>
        go.GetComponent<ValuableObject>() != null && go.GetComponent<Rigidbody>() != null;

    private static GameObject Build(GameObject template)
    {
        var root = Object.Instantiate(template, Holder);
        root.name = Name;

        var physicsMaterial = GetPhysicsMaterial(template.GetComponentInChildren<Collider>(true));

        foreach (var child in root.transform.Cast<Transform>().ToList())
        {
            Object.DestroyImmediate(child.gameObject);
        }

        var objectNode = new GameObject("Object").transform;
        objectNode.SetParent(root.transform, false);

        BuildVisuals(objectNode);
        BuildColliders(objectNode, physicsMaterial);
        ApplyValuableSettings(root);

        root.AddComponent<TokenMarker>();
        return root;
    }

    private static void BuildVisuals(Transform objectNode)
    {
        var meshes = ObjLoader.Load(EmbeddedAssets.ReadText($"{Folder}/token.obj"));

        var visual = new GameObject("token").transform;
        visual.SetParent(objectNode, false);
        visual.localScale = Vector3.one * Scale;
        visual.localPosition = new Vector3(0f, -ModelCentreY * Scale, 0f);

        // OBJ group -> texture (base also holds the wood ring; the gem texture has the Fortnite F baked in).
        foreach (var (part, texture) in new[] { ("base", "base.png"), ("spike", "spike.png"), ("gem", "gem.png") })
        {
            if (!meshes.TryGetValue(part, out var mesh))
            {
                throw new KeyNotFoundException($"token.obj has no part '{part}'. Parts: {string.Join(", ", meshes.Keys)}");
            }

            var go = new GameObject(part);
            go.transform.SetParent(visual, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = CreateMaterial(part, texture);
        }
    }

    private static Material CreateMaterial(string name, string texturePath)
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = name, color = Color.white, mainTexture = EmbeddedAssets.LoadTexture($"{Folder}/{texturePath}") };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.3f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.5f);
        return material;
    }

    // The collider material type was renamed between Unity versions, so it is copied via reflection.
    private static readonly System.Reflection.PropertyInfo? SharedMaterialProperty = typeof(Collider).GetProperty("sharedMaterial");

    private static object? GetPhysicsMaterial(Collider? collider) =>
        collider == null ? null : SharedMaterialProperty?.GetValue(collider);

    private static void BuildColliders(Transform objectNode, object? physicsMaterial)
    {
        var layer = LayerMask.NameToLayer("PhysGrabObject");

        // Two boxes, one turned 45 degrees, approximate the round disc.
        var size = new Vector3(0.27f, 0.075f, 0.27f);
        for (var i = 0; i < 2; i++)
        {
            var go = new GameObject($"Valuable Collider ({i})") { layer = layer == -1 ? 0 : layer, tag = "Phys Grab Object" };
            go.transform.SetParent(objectNode, false);
            go.transform.localRotation = Quaternion.Euler(0f, 45f * i, 0f);

            var box = go.AddComponent<BoxCollider>();
            box.size = i == 0 ? size : new Vector3(size.x * 0.8f, size.y, size.z * 0.8f);
            go.AddComponent<PhysGrabObjectBoxCollider>();
            if (physicsMaterial != null) SharedMaterialProperty?.SetValue(box, physicsMaterial);
            go.AddComponent<PhysGrabObjectCollider>().colliderID = i;
        }
    }

    private static void ApplyValuableSettings(GameObject root)
    {
        var valuable = root.GetComponent<ValuableObject>()
                       ?? throw new System.InvalidOperationException("Template has no ValuableObject.");

        // Worth nothing.
        var value = ScriptableObject.CreateInstance<Value>();
        value.name = $"{Name} value";
        value.valueMin = 0f;
        value.valueMax = 0f;
        value.hideFlags = HideFlags.DontUnloadUnusedAsset;
        valuable.valuePreset = value;

        // Indestructible: no fragility, full durability, and breaking disabled outright.
        var durability = ScriptableObject.CreateInstance<Durability>();
        durability.name = $"{Name} durability";
        durability.fragility = 0f;
        durability.durability = 100f;
        durability.hideFlags = HideFlags.DontUnloadUnusedAsset;
        valuable.durabilityPreset = durability;

        foreach (var detector in root.GetComponentsInChildren<PhysGrabObjectImpactDetector>(true))
        {
            detector.destroyDisable = true;
        }

        valuable.physAttributePreset = PresetLibrary.Find<PhysAttribute>("phys", "PhysAttribute Medium") ?? valuable.physAttributePreset;
        valuable.audioPreset = PresetLibrary.Find<PhysAudio>("audio", "PhysAudio - Gem Burger") ?? valuable.audioPreset;
        valuable.volumeType = ValuableVolume.Type.Tiny;
        valuable.particleColors = new Gradient
        {
            colorKeys = new[]
            {
                new GradientColorKey(new Color(0.95f, 0.6f, 0.15f), 0f),
                new GradientColorKey(new Color(0.55f, 0.3f, 0.1f), 1f),
            },
            alphaKeys = new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, 1f) },
            mode = GradientMode.Fixed,
        };
    }
}
