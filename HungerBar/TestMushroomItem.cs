using System.Collections;
using HarmonyLib;
using GameNetcodeStuff;
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
                player.ItemSlots[i].itemProperties.itemName == "Test mushroom")
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

        try
        {
            GrabbableObject mushroom = MushroomFactory.Create(player);
            player.SwitchToItemSlot(slot, mushroom);
            Plugin.Log.LogInfo($"Test low-poly mushroom placed into inventory slot {slot}");
        }
        catch (System.Exception exception)
        {
            Plugin.Log.LogError($"Failed to create test mushroom: {exception}");
        }
    }
}

internal static class MushroomFactory
{
    internal static GrabbableObject Create(PlayerControllerB player)
    {
        GameObject root = new("TestLowPolyMushroom");
        root.transform.position = player.transform.position;
        root.layer = 6;

        GrabbableObject grabbable = root.AddComponent<GrabbableObject>();
        Item properties = ScriptableObject.CreateInstance<Item>();
        properties.itemName = "Test mushroom";
        properties.canBeGrabbedBeforeGameStart = true;
        properties.twoHanded = false;
        properties.weight = 1.05f;
        properties.positionOffset = new Vector3(0.05f, 0.08f, -0.12f);
        properties.rotationOffset = new Vector3(5f, 0f, -12f);
        properties.restingRotation = new Vector3(0f, 0f, 90f);
        properties.verticalOffset = 0.08f;
        grabbable.itemProperties = properties;

        Material stemMaterial = MakeMaterial(new Color(0.55f, 0.48f, 0.34f, 1f), 0.15f);
        Material capMaterial = MakeMaterial(new Color(0.67f, 0.16f, 0.06f, 1f), 0.1f);
        Material glowMaterial = MakeMaterial(new Color(0.36f, 0.85f, 0.18f, 1f), 0.3f);

        CreateMeshPart("Stem", root.transform,
            LowPolyMeshes.CreateTaperedCylinder(8, 0.13f, 0.19f, 0.52f),
            stemMaterial, new Vector3(0f, 0.25f, 0f));
        CreateMeshPart("Cap", root.transform,
            LowPolyMeshes.CreateCap(10, 3, 0.38f, 0.20f),
            capMaterial, new Vector3(0f, 0.56f, 0f));

        Vector3[] spots =
        {
            new(-0.17f, 0.70f, -0.08f), new(0.16f, 0.68f, -0.10f),
            new(0.04f, 0.75f, 0.09f), new(-0.25f, 0.62f, 0.05f)
        };
        foreach (Vector3 position in spots)
        {
            GameObject spot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            spot.name = "BioluminescentSpot";
            spot.transform.SetParent(root.transform, false);
            spot.transform.localPosition = position;
            spot.transform.localScale = new Vector3(0.055f, 0.025f, 0.055f);
            spot.GetComponent<Renderer>().sharedMaterial = glowMaterial;
            Object.Destroy(spot.GetComponent<Collider>());
        }

        CapsuleCollider collider = root.AddComponent<CapsuleCollider>();
        collider.center = new Vector3(0f, 0.34f, 0f);
        collider.height = 0.75f;
        collider.radius = 0.28f;

        grabbable.fallTime = 1f;
        grabbable.hasHitGround = true;
        grabbable.EnablePhysics(false);
        return grabbable;
    }

    private static void CreateMeshPart(string name, Transform parent, Mesh mesh, Material material, Vector3 position)
    {
        GameObject part = new(name, typeof(MeshFilter), typeof(MeshRenderer));
        part.transform.SetParent(parent, false);
        part.transform.localPosition = position;
        part.GetComponent<MeshFilter>().sharedMesh = mesh;
        part.GetComponent<MeshRenderer>().sharedMaterial = material;
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
