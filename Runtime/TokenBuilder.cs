using System.Collections.Generic;
using System.Linq;
using REPOLib.Modules;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FortnitePirateToken.Runtime;

/// <summary>
/// Builds the Pirate Shop Token valuable prefab at runtime from a vanilla valuable (which provides the networking and
/// physics components) and registers it with REPOLib. The spawn patch keeps it out of the normal valuable pool and
/// rolls for it separately.
/// </summary>
internal static class TokenBuilder
{
    public const string Name = "Pirate Shop Token";
    private const string Folder = "token";

    // The model is ~0.194 m wide. 4.5x makes it ~0.87 m, as wide as the Wizard Cube of Knowledge (~0.88 m).
    private const float Scale = 4.5f;

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
            // Registered in the generic preset so it shows up in REPOLib's valuable list (spawn mods use it).
            // TokenSpawnPatch stops the director from ever picking it by itself.
            Prefab = Valuables.RegisterValuable(prefab, new List<string> { "Valuables - Generic" });
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
        AddGlowAndSparks(objectNode);
        ApplyValuableSettings(root);

        root.AddComponent<TokenMarker>();
        root.AddComponent<TokenReveal>();
        return root;
    }

    private static void BuildVisuals(Transform objectNode)
    {
        var meshes = ObjLoader.Load(EmbeddedAssets.ReadText($"{Folder}/token.obj"));

        var visual = new GameObject("token").transform;
        visual.SetParent(objectNode, false);
        visual.localScale = Vector3.one * Scale;
        visual.localPosition = new Vector3(0f, -ModelCentreY * Scale, 0f);

        // The gem's UVs are a stretched dome unwrap, which distorts the F. Project it straight down instead, the same
        // way the Blender material places it (bounding-box "generated" coordinates, over the dome and its rim).
        ProjectFromTop(meshes["gem"], meshes["gem_rim"]);

        // OBJ group -> texture (base also holds the wood ring; the gem texture has the Fortnite F baked in).
        foreach (var (part, texture) in new[] { ("base", "base.png"), ("spike", "spike.png"), ("gem", "gem.png"), ("gem_rim", "") })
        {
            if (!meshes.TryGetValue(part, out var mesh))
            {
                throw new KeyNotFoundException($"token.obj has no part '{part}'. Parts: {string.Join(", ", meshes.Keys)}");
            }

            var go = new GameObject(part);
            go.transform.SetParent(visual, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = texture.Length == 0
                ? CreateColorMaterial(part, new Color(0.5f, 0.28f, 0.12f))
                : CreateMaterial(part, texture, glow: part == "gem");
        }
    }

    private static void ProjectFromTop(Mesh dome, Mesh rim)
    {
        var bounds = dome.bounds;
        bounds.Encapsulate(rim.bounds);

        // The loader mirrors X, so the original Blender X is -x here; Blender Y is -z.
        var vertices = dome.vertices;
        var uvs = new Vector2[vertices.Length];
        for (var i = 0; i < vertices.Length; i++)
        {
            uvs[i] = new Vector2(
                (bounds.max.x - vertices[i].x) / bounds.size.x,
                (bounds.max.z - vertices[i].z) / bounds.size.z);
        }

        dome.uv = uvs;
    }

    private static Material CreateColorMaterial(string name, Color color)
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = name, color = color };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.3f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.5f);
        return material;
    }

    private static Material CreateMaterial(string name, string texturePath, bool glow = false)
    {
        var shader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Sprites/Default");
        var material = new Material(shader) { name = name, color = Color.white, mainTexture = EmbeddedAssets.LoadTexture($"{Folder}/{texturePath}") };
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0.3f);
        if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.5f);
        if (glow && material.HasProperty("_EmissionColor"))
        {
            material.EnableKeyword("_EMISSION");
            material.SetTexture("_EmissionMap", material.mainTexture);
            material.SetColor("_EmissionColor", new Color(0.55f, 0.55f, 0.55f));
        }

        return material;
    }

    private static readonly Color GoldLight = new(1f, 0.72f, 0.25f);

    /// <summary>A golden point light, a soft pulsing halo and rising sparks.</summary>
    private static void AddGlowAndSparks(Transform parent)
    {
        var fx = new GameObject("Glow FX").transform;
        fx.SetParent(parent, false);

        var light = fx.gameObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = GoldLight;
        light.range = 4f;
        light.intensity = 2f;
        light.shadows = LightShadows.None;

        var material = CreateParticleMaterial();
        AddHalo(fx, material);
        AddSparks(fx, material);
    }

    private static Material CreateParticleMaterial()
    {
        // Additive looks best; otherwise plain alpha blending. Particles/Standard Unlit is last because it renders opaque
        // (a solid square) unless a shader variant the build may have stripped is switched on.
        var shader = Shader.Find("Legacy Shaders/Particles/Additive")
                     ?? Shader.Find("Sprites/Default") // alpha blended without keywords, so no stripped variants
                     ?? Shader.Find("Particles/Standard Unlit")
                     ?? Shader.Find("Standard");
        FortnitePirateTokenPlugin.Logger.LogInfo($"Token particles use shader '{shader.name}'.");

        var material = new Material(shader) { name = "token glow", mainTexture = SoftDotTexture() };
        if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));

        // The Standard particle shaders start out opaque, which ignores the texture's alpha and draws a solid square.
        // Switch them to additive blending (what the shader's own "Additive" blend mode does).
        if (material.HasProperty("_Mode") || material.HasProperty("_SrcBlend"))
        {
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 4f);
            if (material.HasProperty("_SrcBlend")) material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            if (material.HasProperty("_ZWrite")) material.SetFloat("_ZWrite", 0f);
            material.SetOverrideTag("RenderType", "Transparent");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        return material;
    }

    // A white dot that fades out towards its edge.
    private static Texture2D SoftDotTexture()
    {
        const int size = 64;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "soft dot", wrapMode = TextureWrapMode.Clamp };
        var pixels = new Color[size * size];
        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(size / 2f, size / 2f)) / (size / 2f);
                var a = Mathf.Clamp01(1f - d);
                a *= a;
                pixels[y * size + x] = new Color(1f, 1f, 1f, a);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        return texture;
    }

    private static Gradient FadeInOut(Color color, float peakAlpha)
    {
        return new Gradient
        {
            colorKeys = new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            alphaKeys = new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(peakAlpha, 0.5f),
                new GradientAlphaKey(0f, 1f),
            },
        };
    }

    // Overlapping slow pulses of one big soft particle add up to a steady halo around the token.
    private static void AddHalo(Transform parent, Material material)
    {
        var go = new GameObject("Halo");
        go.transform.SetParent(parent, false);
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.startLifetime = 2f;
        main.startSpeed = 0f;
        main.startSize = 1.7f;
        main.startColor = new Color(1f, 0.75f, 0.3f, 1f);
        main.maxParticles = 8;

        var emission = system.emission;
        emission.rateOverTime = 2f;

        var shape = system.shape;
        shape.enabled = false;

        var color = system.colorOverLifetime;
        color.enabled = true;
        color.color = FadeInOut(Color.white, 0.3f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        system.Play();
    }

    private static void AddSparks(Transform parent, Material material)
    {
        var go = new GameObject("Sparks");
        go.transform.SetParent(parent, false);
        go.transform.localRotation = Quaternion.Euler(-90f, 0f, 0f); // cone shoots along +Z, so this is straight up
        var system = go.AddComponent<ParticleSystem>();
        system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        var main = system.main;
        main.loop = true;
        main.playOnAwake = true;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.9f, 1.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.15f, 0.55f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
        main.startColor = new ParticleSystem.MinMaxGradient(new Color(1f, 0.92f, 0.55f, 1f), new Color(1f, 0.6f, 0.15f, 1f));
        main.gravityModifier = -0.03f;
        main.maxParticles = 80;

        var emission = system.emission;
        emission.rateOverTime = 20f;

        var shape = system.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 25f;
        shape.radius = 0.4f;
        shape.radiusThickness = 1f;

        var noise = system.noise;
        noise.enabled = true;
        noise.strength = 0.15f;
        noise.frequency = 1.5f;

        var size = system.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

        var color = system.colorOverLifetime;
        color.enabled = true;
        color.color = FadeInOut(Color.white, 1f);

        var renderer = go.GetComponent<ParticleSystemRenderer>();
        renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;

        system.Play();
    }

    // The collider material type was renamed between Unity versions, so it is copied via reflection.
    private static readonly System.Reflection.PropertyInfo? SharedMaterialProperty = typeof(Collider).GetProperty("sharedMaterial");

    private static object? GetPhysicsMaterial(Collider? collider) =>
        collider == null ? null : SharedMaterialProperty?.GetValue(collider);

    private static void BuildColliders(Transform objectNode, object? physicsMaterial)
    {
        var layer = LayerMask.NameToLayer("PhysGrabObject");

        // Two boxes, one turned 45 degrees, approximate the round disc (model is 0.194 wide, 0.045 tall).
        var size = new Vector3(0.185f * Scale, 0.05f * Scale, 0.185f * Scale);
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
        valuable.volumeType = ValuableVolume.Type.Big;
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
