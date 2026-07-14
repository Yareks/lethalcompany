using System.Collections;
using HarmonyLib;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;

namespace HungerBar;

/// <summary>Creates a local test item when the local player enters a lobby.</summary>
[HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
internal static class GiveMushroomOnJoinPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerControllerB __instance)
    {
        if (GameNetworkManager.Instance == null ||
            __instance != GameNetworkManager.Instance.localPlayerController)
            return;

        __instance.StartCoroutine(GiveAfterHudIsReady(__instance));
    }

    private static IEnumerator GiveAfterHudIsReady(PlayerControllerB player)
    {
        yield return new WaitForSeconds(1.5f);

        if (player == null)
            yield break;

        for (int i = 0; i < player.ItemSlots.Length; i++)
        {
            if (player.ItemSlots[i] != null && player.ItemSlots[i].itemProperties != null &&
                player.ItemSlots[i].itemProperties.itemName == "Mushroom")
            {
                Plugin.Log.LogInfo("Test mushroom is already in the inventory");
                yield break;
            }
        }

        int slot = -1;
        for (int i = 0; i < player.ItemSlots.Length; i++)
        {
            if (player.ItemSlots[i] == null)
            {
                slot = i;
                break;
            }
        }

        if (slot < 0)
        {
            Plugin.Log.LogWarning("Cannot give test mushroom: inventory is full");
            yield break;
        }

        GrabbableObject? mushroom = null;
        bool creationFailed = false;
        try
        {
            mushroom = MushroomFactory.Create(player);
            NetworkObject networkObject = mushroom.GetComponent<NetworkObject>();

            // Use the game's complete pickup RPC instead of writing ItemSlots manually.
            // It initializes playerHeldBy, parentObject, held flags and animation state.
            System.Reflection.MethodInfo? grabMethod = AccessTools.Method(
                typeof(PlayerControllerB), "GrabObjectServerRpc",
                new[] { typeof(NetworkObjectReference) });
            if (grabMethod == null)
                throw new System.MissingMethodException("PlayerControllerB.GrabObjectServerRpc");

            grabMethod.Invoke(player, new object[] { new NetworkObjectReference(networkObject) });
            Plugin.Log.LogInfo($"Test low-poly mushroom pickup requested for inventory slot {slot}");
        }
        catch (System.Exception exception)
        {
            creationFailed = true;
            Plugin.Log.LogError($"Failed to create test mushroom: {exception}");
        }

        if (creationFailed || mushroom == null)
            yield break;

        // The pickup RPC fills the inventory correctly but does not always select the
        // newly occupied slot. Wait for its ClientRpc, then equip it explicitly.
        yield return new WaitForSeconds(0.75f);
        try
        {
            int actualSlot = System.Array.IndexOf(player.ItemSlots, mushroom);
            if (actualSlot < 0)
            {
                // This game version acknowledges the pickup RPC but does not insert a
                // runtime-cloned Item into ItemSlots. The object is now a valid spawned
                // network prefab, so completing the slot assignment locally is safe.
                actualSlot = slot;
                player.ItemSlots[actualSlot] = mushroom;
                Plugin.Log.LogWarning($"Pickup RPC did not fill ItemSlots; applying safe local fallback to slot {actualSlot}");
            }

            // Complete the normal held-item state before switching. Without parentObject,
            // the mesh remains at its world position instead of following the hand.
            mushroom!.playerHeldBy = player;
            mushroom.parentObject = player.localItemHolder;
            mushroom.transform.SetParent(player.localItemHolder, false);
            mushroom.isHeld = true;
            mushroom.isPocketed = false;
            mushroom.EnablePhysics(false);

            System.Reflection.MethodInfo? switchMethod = AccessTools.Method(
                typeof(PlayerControllerB), "SwitchToItemSlot",
                new[] { typeof(int), typeof(GrabbableObject) });
            if (switchMethod == null)
                throw new System.MissingMethodException("PlayerControllerB.SwitchToItemSlot");

            switchMethod.Invoke(player, new object[] { actualSlot, mushroom! });
            Plugin.Log.LogInfo($"Mushroom equipped: slot={actualSlot}, held={mushroom!.isHeld}, pocketed={mushroom.isPocketed}, parent={(mushroom.parentObject == null ? "null" : mushroom.parentObject.name)}");
        }
        catch (System.Exception exception)
        {
            Plugin.Log.LogError($"Failed to equip test mushroom: {exception}");
        }
    }
}

internal static class MushroomFactory
{
    internal static GrabbableObject Create(PlayerControllerB player)
    {
        Item? template = null;
        foreach (Item item in StartOfRound.Instance.allItemsList.itemsList)
        {
            if (item == null || item.spawnPrefab == null)
                continue;

            GrabbableObject candidate = item.spawnPrefab.GetComponent<GrabbableObject>();
            if (candidate != null && candidate.GetType().Name == "PhysicsProp" &&
                item.spawnPrefab.GetComponent<Unity.Netcode.NetworkObject>() != null)
            {
                // PhysicsProp is the game's neutral, ordinary inventory scrap type.
                // Equipment such as Binoculars/BeltBag uses special slots and actions.
                template = item;
                break;
            }
        }

        if (template == null)
            throw new System.InvalidOperationException("No networked grabbable item prefab was found");

        GameObject root = Object.Instantiate(template.spawnPrefab, player.transform.position, Quaternion.identity);
        root.name = "TestLowPolyMushroom";
        root.layer = 6; // Props layer rendered by the first-person item camera.
        GrabbableObject grabbable = root.GetComponent<GrabbableObject>();
        Unity.Netcode.NetworkObject networkObject = root.GetComponent<Unity.Netcode.NetworkObject>();

        // Keep all mandatory prefab references and animation data, changing only identity and visuals.
        Item properties = Object.Instantiate(template);
        properties.itemName = "Mushroom";
        properties.canBeGrabbedBeforeGameStart = true;
        properties.twoHanded = false;
        properties.disallowUtilitySlot = true; // Force ordinary numbered inventory, never TAB/belt.
        properties.isScrap = false; // Food, not sellable scrap.
        properties.holdButtonUse = true;
        properties.weight = 1.05f;
        properties.positionOffset = new Vector3(0f, -0.08f, 0.02f);
        properties.rotationOffset = Vector3.zero;
        properties.restingRotation = Vector3.zero; // Y-up model stands on its stem.
        properties.verticalOffset = 0.26f;
        properties.floorYOffset = 0.26f;
        properties.toolTips = new[] { "Eat : [LMB]" };
        properties.itemIcon = MushroomIcon.Load();
        grabbable.itemProperties = properties;

        Renderer[] originalRenderers = root.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer renderer in originalRenderers)
            renderer.enabled = false;

        Material stemMaterial = MakeMaterial(new Color(0.55f, 0.48f, 0.34f, 1f), 0.15f);
        Material capMaterial = MakeMaterial(new Color(0.67f, 0.16f, 0.06f, 1f), 0.1f);
        Material glowMaterial = MakeMaterial(new Color(0.36f, 0.85f, 0.18f, 1f), 0.3f);

        // Separate visual pivot lets held and floor presentation use different scale/offset.
        GameObject visualRoot = new("MushroomVisualRoot");
        visualRoot.transform.SetParent(root.transform, false);
        visualRoot.layer = root.layer;

        // Origin sits around the grip point so the hand wraps around the stem.
        GameObject stem = CreateMeshPart("MushroomStem", visualRoot.transform,
            LowPolyMeshes.CreateTaperedCylinder(8, 0.085f, 0.13f, 0.40f),
            stemMaterial, new Vector3(0f, -0.16f, 0f), root.layer);
        GameObject cap = CreateMeshPart("MushroomCap", visualRoot.transform,
            LowPolyMeshes.CreateCap(12, 3, 0.27f, 0.15f),
            capMaterial, new Vector3(0f, 0.23f, 0f), root.layer);

        System.Collections.Generic.List<Renderer> mushroomRenderers = new()
        {
            stem.GetComponent<Renderer>(), cap.GetComponent<Renderer>()
        };

        Vector3[] spots =
        {
            new(-0.12f, 0.34f, -0.06f), new(0.12f, 0.33f, -0.07f),
            new(0.03f, 0.38f, 0.07f), new(-0.18f, 0.29f, 0.04f)
        };
        foreach (Vector3 position in spots)
        {
            GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spot.name = "MushroomGlowSpot";
            spot.transform.SetParent(visualRoot.transform, false);
            spot.transform.localPosition = position;
            spot.transform.localScale = new Vector3(0.055f, 0.025f, 0.055f);
            spot.layer = root.layer;
            Renderer spotRenderer = spot.GetComponent<Renderer>();
            spotRenderer.sharedMaterial = glowMaterial;
            mushroomRenderers.Add(spotRenderer);
            Object.Destroy(spot.GetComponent<Collider>());
        }

        ScanNodeProperties scanNode = root.GetComponentInChildren<ScanNodeProperties>(true);
        if (scanNode != null)
        {
            scanNode.headerText = "Mushroom";
            scanNode.subText = "Food";
            scanNode.scrapValue = 0;
        }

        MushroomVisualController visualController = root.AddComponent<MushroomVisualController>();
        visualController.Initialize(grabbable, visualRoot.transform, mushroomRenderers.ToArray(), originalRenderers);
        root.AddComponent<MushroomFoodController>().Initialize(grabbable);

        // A valid NetworkObject is essential: inventory drop and switch RPCs pass its reference.
        if (!networkObject.IsSpawned)
            networkObject.Spawn(false);

        Plugin.Log.LogInfo($"Mushroom made from procedural low-poly meshes: renderers={mushroomRenderers.Count}, layer={root.layer}");
        Plugin.Log.LogInfo($"Mushroom cloned from network prefab '{template.itemName}', networkId={networkObject.NetworkObjectId}");
        return grabbable;
    }

    private static GameObject CreateMeshPart(string name, Transform parent, Mesh mesh, Material material, Vector3 position, int layer)
    {
        GameObject part = new(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.layer = layer;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
        return part;
    }

    private static Material MakeMaterial(Color color, float metallic)
    {
        Shader shader = Shader.Find("HDRP/Lit") ?? Shader.Find("Standard");
        Material material = new(shader);
        material.color = color;
        if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
        if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", 0.18f);
        return material;
    }
}

internal static class MushroomIcon
{
    private static Sprite? _sprite;

    internal static Sprite Load()
    {
        if (_sprite != null)
            return _sprite;

        using System.IO.Stream? stream = typeof(MushroomIcon).Assembly
            .GetManifestResourceStream("HungerBar.Resources.lethal_mushroom.png");
        if (stream == null)
            throw new System.IO.FileNotFoundException("Embedded mushroom icon was not found");

        byte[] bytes = new byte[stream.Length];
        stream.Read(bytes, 0, bytes.Length);
        Texture2D texture = new(2, 2, TextureFormat.RGBA32, false)
        {
            name = "MushroomIconTexture",
            filterMode = FilterMode.Bilinear
        };
        if (!texture.LoadImage(bytes, false))
            throw new System.InvalidOperationException("Mushroom icon PNG could not be decoded");

        _sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height),
            new Vector2(0.5f, 0.5f), 100f);
        _sprite.name = "MushroomIcon";
        return _sprite;
    }
}

[DefaultExecutionOrder(32000)]
internal sealed class MushroomVisualController : MonoBehaviour
{
    private GrabbableObject? _item;
    private Transform? _visualRoot;
    private Renderer[] _renderers = System.Array.Empty<Renderer>();
    private Renderer[] _originalRenderers = System.Array.Empty<Renderer>();
    private bool _lastVisible = true;
    private bool _lastHeld;

    internal void Initialize(GrabbableObject item, Transform visualRoot, Renderer[] renderers, Renderer[] originalRenderers)
    {
        _item = item;
        _visualRoot = visualRoot;
        _renderers = renderers;
        _originalRenderers = originalRenderers;
        UpdatePresentation(true);
    }

    private void LateUpdate()
    {
        if (_item == null)
            return;

        // The base prefab only knows about its original renderer. Keep our generated
        // mesh in sync when the item is selected or pocketed.
        foreach (Renderer original in _originalRenderers)
            if (original != null && original.enabled)
                original.enabled = false;

        bool visible = !_item.isPocketed;
        if (visible != _lastVisible || _item.isHeld != _lastHeld)
            UpdatePresentation(visible);
    }

    private void UpdatePresentation(bool visible)
    {
        _lastHeld = _item != null && _item.isHeld;
        if (_visualRoot != null)
        {
            // Normal size in hand; 30% larger and upright when lying in the world.
            _visualRoot.localScale = _lastHeld ? Vector3.one : Vector3.one * 1.6f;
            _visualRoot.localPosition = _lastHeld ? new Vector3(0f, 0.12f, 0f) : Vector3.zero;
            _visualRoot.localRotation = Quaternion.identity;
        }
        SetVisible(visible);
    }

    private void SetVisible(bool visible)
    {
        _lastVisible = visible;
        foreach (Renderer renderer in _renderers)
        {
            if (renderer != null)
            {
                renderer.gameObject.layer = 6;
                renderer.enabled = visible;
            }
        }
    }
}

internal sealed class MushroomFoodController : MonoBehaviour
{
    private const float EatDuration = 1.5f;
    private GrabbableObject? _item;
    private float _heldTime;
    private bool _started;

    internal void Initialize(GrabbableObject item) => _item = item;

    private void Update()
    {
        if (_item == null || !_item.isHeld || _item.playerHeldBy == null ||
            GameNetworkManager.Instance == null ||
            _item.playerHeldBy != GameNetworkManager.Instance.localPlayerController)
        {
            ResetEating();
            return;
        }

        if (!Input.GetMouseButton(0))
        {
            ResetEating();
            return;
        }

        if (!_started)
        {
            _started = true;
            Plugin.Log.LogInfo("Eating mushroom started; hold LMB for 1.5 seconds");
        }

        _heldTime += Time.unscaledDeltaTime;
        if (_heldTime < EatDuration)
            return;

        PlayerControllerB player = _item.playerHeldBy;
        Plugin.Refill(0.25f);
        Plugin.Log.LogInfo("Mushroom eaten: hunger restored by 25 points");
        _item.DestroyObjectInHand(player);
        enabled = false;
    }

    private void ResetEating()
    {
        _heldTime = 0f;
        _started = false;
    }
}

internal static class LowPolyMeshes
{
    internal static Mesh CreateTaperedCylinder(int sides, float bottomRadius, float topRadius, float height)
    {
        Vector3[] vertices = new Vector3[sides * 2 + 2];
        for (int i = 0; i < sides; i++)
        {
            float angle = Mathf.PI * 2f * i / sides;
            vertices[i] = new Vector3(Mathf.Cos(angle) * bottomRadius, 0f, Mathf.Sin(angle) * bottomRadius);
            vertices[i + sides] = new Vector3(Mathf.Cos(angle) * topRadius, height, Mathf.Sin(angle) * topRadius);
        }
        vertices[sides * 2] = Vector3.zero;
        vertices[sides * 2 + 1] = new Vector3(0f, height, 0f);

        int[] triangles = new int[sides * 12];
        int t = 0;
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            triangles[t++] = i; triangles[t++] = i + sides; triangles[t++] = next + sides;
            triangles[t++] = i; triangles[t++] = next + sides; triangles[t++] = next;
            triangles[t++] = sides * 2; triangles[t++] = next; triangles[t++] = i;
            triangles[t++] = sides * 2 + 1; triangles[t++] = i + sides; triangles[t++] = next + sides;
        }
        return Build("LowPolyStem", vertices, triangles);
    }

    internal static Mesh CreateCap(int sides, int rings, float radius, float height)
    {
        Vector3[] vertices = new Vector3[(rings + 1) * sides + 1];
        for (int ring = 0; ring <= rings; ring++)
        {
            float p = ring / (float)rings;
            float ringRadius = radius * Mathf.Sin(p * Mathf.PI * 0.5f);
            float y = height * (1f - p * p);
            for (int i = 0; i < sides; i++)
            {
                float angle = Mathf.PI * 2f * i / sides;
                vertices[ring * sides + i] = new Vector3(Mathf.Cos(angle) * ringRadius, y, Mathf.Sin(angle) * ringRadius);
            }
        }
        int bottomCenter = vertices.Length - 1;
        int[] triangles = new int[rings * sides * 6 + sides * 3];
        int t = 0;
        for (int ring = 0; ring < rings; ring++)
        for (int i = 0; i < sides; i++)
        {
            int next = (i + 1) % sides;
            int a = ring * sides + i;
            int b = ring * sides + next;
            int c = (ring + 1) * sides + i;
            int d = (ring + 1) * sides + next;
            triangles[t++] = a; triangles[t++] = c; triangles[t++] = d;
            triangles[t++] = a; triangles[t++] = d; triangles[t++] = b;
        }
        int lastRing = rings * sides;
        for (int i = 0; i < sides; i++)
        {
            triangles[t++] = bottomCenter;
            triangles[t++] = lastRing + (i + 1) % sides;
            triangles[t++] = lastRing + i;
        }
        return Build("LowPolyCap", vertices, triangles);
    }

    private static Mesh Build(string name, Vector3[] vertices, int[] triangles)
    {
        Mesh mesh = new() { name = name, vertices = vertices, triangles = triangles };
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        return mesh;
    }
}
