using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DV;
using DV.CabControls;
using DV.Highlighting;
using DV.Interaction;
using DV.InventorySystem;
using DV.Utils;
using UnityEngine;

namespace AutoTrashToss
{
    // Runs every frame. When you hold a trashable item and look at a trash can (ItemDumpster),
    // it highlights the bin, shows a prompt, and lets you click to auto-toss the item in.
    public class TrashTosser : MonoBehaviour
    {
        // Time the item spends visibly falling into the bin before it's disposed.
        private const float FALL_TIME = 0.4f;

        // >>> How far ABOVE the bin's opening the item spawns before it drops in (metres). <<<
        // Increase = spawns higher; decrease = spawns lower / closer to the rim.
        private const float DROP_HEIGHT_ABOVE_OPENING = -0.5f;

        // How far outside the bin's mesh a look-at still counts (metres). Larger = more
        // forgiving aim but easier to trigger on nearby objects; smaller = stricter.
        private const float AIM_TOLERANCE = 0.0f;

        private static readonly int TINT_COLOR = Shader.PropertyToID("_TintColor");

        // ItemDumpster keeps its logic private; we mirror its rules and reuse its action.
        private static readonly MethodInfo IsValidMethod =
            typeof(ItemDumpster).GetMethod("IsValidDumpsterItem", BindingFlags.Instance | BindingFlags.NonPublic);
        private static readonly MethodInfo RegisterMethod =
            typeof(ItemDumpster).GetMethod("RegisterItem", BindingFlags.Instance | BindingFlags.NonPublic);

        // Prefab names of every actual license item (not the info samples).
        private static HashSet<string> licenseNames;

        private Grabber grabber;
        private int rayMask = 0; // 0 = not computed yet
        private AGrabHandler heldLastFrame; // to ignore the click that grabbed an item

        // Highlight state (either an outline or a cloned loco glow).
        private ItemDumpster highlightedDumpster;
        private Renderer[] outlineRenderers;
        private GameObject glowInstance;
        private MaterialPropertyBlock glowBlock;
        private Color glowBaseColor = Color.white;
        private bool glowBaseCaptured;

        private void Update()
        {
            if (VRManager.IsVREnabled()) { ClearHighlight(); heldLastFrame = null; return; }
            if (!TryGetGrabber()) { ClearHighlight(); heldLastFrame = null; return; }

            AGrabHandler held = grabber.CurrentItemHeld;

            // If the item only became held this frame, the click that grabbed it is the same
            // click we'd read as a toss. Ignore tossing until we've held it for a frame, so
            // picking an item back out of the bin doesn't instantly throw it in again.
            bool freshlyGrabbed = held != null && held != heldLastFrame;
            heldLastFrame = held;

            ItemBase heldItem = GetItem(held);
            if (heldItem == null) { ClearHighlight(); return; }

            Camera cam = PlayerManager.ActiveCamera;
            if (cam == null) { ClearHighlight(); return; }

            ItemDumpster dumpster = RaycastDumpster(cam, Main.Settings.maxDistance);
            if (dumpster == null || !IsTrashable(dumpster, heldItem)) { ClearHighlight(); return; }

            if (Main.Settings.enableHighlight)
                ApplyHighlight(dumpster, cam);
            else
                ClearHighlight();

            if (Main.Settings.showPrompt)
                ShowPrompt();

            if (TossPressed() && !freshlyGrabbed)
            {
                TossItem(dumpster, held, heldItem);
                ClearHighlight();
            }
        }

        private bool TryGetGrabber()
        {
            if (grabber != null) return true;
            if (PlayerManager.PlayerTransform == null) return false;
            grabber = PlayerManager.PlayerTransform.GetComponentInChildren<Grabber>(includeInactive: true);
            return grabber != null;
        }

        private static ItemBase GetItem(AGrabHandler held)
        {
            if (held == null) return null;
            return held.GetComponent<ItemBase>() ?? held.GetComponentInParent<ItemBase>();
        }

        private bool TossPressed()
        {
            if (Main.Settings.useLeftClick && Input.GetMouseButtonDown(0)) return true;
            return Main.Settings.altKey != null && Main.Settings.altKey.Down();
        }

        private ItemDumpster RaycastDumpster(Camera cam, float maxDistance)
        {
            if (rayMask == 0)
            {
                int grabbed = LayerMask.NameToLayer("Grabbed_Item");
                rayMask = (grabbed >= 0) ? ~(1 << grabbed) : ~0;
            }

            Ray ray = new Ray(cam.transform.position, cam.transform.forward);
            if (!Physics.Raycast(ray, out RaycastHit hit, maxDistance, rayMask, QueryTriggerInteraction.Collide))
                return null;

            ItemDumpster dumpster = FindDumpster(hit.collider);
            if (dumpster == null) return null;

            // Make sure the point we actually hit is ON this bin, not just something in the
            // same room. Without this, hitting a wall/desk that shares a parent with a bin
            // would falsely resolve to that bin.
            Bounds b = GetBinBounds(dumpster);
            b.Expand(AIM_TOLERANCE);
            return b.Contains(hit.point) ? dumpster : null;
        }

        // The bin's visible mesh collider and its ItemDumpster (on a child trigger) can sit on
        // different objects, so look both UP the parents and DOWN into nearby subtrees.
        private static ItemDumpster FindDumpster(Collider col)
        {
            ItemDumpster d = col.GetComponentInParent<ItemDumpster>();
            if (d != null) return d;

            Transform t = col.transform;
            for (int level = 0; level < 4 && t != null; level++)
            {
                d = t.GetComponentInChildren<ItemDumpster>();
                if (d != null) return d;
                t = t.parent;
            }
            return null;
        }

        // --- What counts as trashable ------------------------------------------

        private static bool IsTrashable(ItemDumpster dumpster, ItemBase item)
        {
            if (Main.Settings.allowJobBooklets && item.GetComponent<JobBooklet>() != null)
                return true;
            if (Main.Settings.allowLicenses && IsLicenseItem(item))
                return true;

            if (IsValidMethod != null)
            {
                try { return (bool)IsValidMethod.Invoke(dumpster, new object[] { item }); }
                catch { return false; }
            }
            return item.InventorySpecs != null && !item.InventorySpecs.ImmuneToDumpster;
        }

        private static bool IsLicenseItem(ItemBase item)
        {
            if (licenseNames == null)
            {
                DV.ThingTypes.DVObjectModel types = (Globals.G != null) ? Globals.G.Types : null;
                if (types == null) return false; // not ready yet; try again next time
                var set = new HashSet<string>();
                if (types.jobLicenses != null)
                    foreach (var l in types.jobLicenses)
                        if (l != null && l.licensePrefab != null) set.Add(l.licensePrefab.name);
                if (types.generalLicenses != null)
                    foreach (var l in types.generalLicenses)
                        if (l != null && l.licensePrefab != null) set.Add(l.licensePrefab.name);
                licenseNames = set;
            }

            string n = item.gameObject.name;
            int clone = n.IndexOf("(Clone)");
            if (clone >= 0) n = n.Substring(0, clone);
            return licenseNames.Contains(n.Trim());
        }

        // --- Prompt -------------------------------------------------------------

        private void ShowPrompt()
        {
            var controller = SingletonBehaviour<InteractionTextControllerNonVr>.Instance;
            if (controller != null)
                controller.DisplayText("Throw away  [" + TossButtonLabel() + "]");
        }

        private string TossButtonLabel()
        {
            if (!Main.Settings.useLeftClick && Main.Settings.altKey != null && Main.Settings.altKey.keyCode != KeyCode.None)
                return Main.Settings.altKey.ToString();
            return "LMB";
        }

        // --- Highlighting -------------------------------------------------------

        private void ApplyHighlight(ItemDumpster dumpster, Camera cam)
        {
            if (Main.Settings.useLocoGlow && TryInitGlow())
                ApplyGlow(dumpster, cam);
            else
                ApplyOutline(dumpster);
        }

        private void ApplyGlow(ItemDumpster dumpster, Camera cam)
        {
            if (highlightedDumpster != dumpster)
            {
                ClearHighlight();
                PositionAndScaleGlow(dumpster);
                BoostGlow();
                glowInstance.SetActive(true);
                highlightedDumpster = dumpster;
            }
            // Billboard toward the camera, exactly like the loco glow does.
            if (cam != null)
                glowInstance.transform.LookAt(cam.transform);
        }

        private void ApplyOutline(ItemDumpster dumpster)
        {
            if (highlightedDumpster == dumpster && outlineRenderers != null) return;
            ClearHighlight();

            AGeneralHighlighter highlighter = SingletonBehaviour<AGeneralHighlighter>.Instance;
            if (highlighter == null) return;

            outlineRenderers = GetBinRenderers(dumpster);
            foreach (Renderer r in outlineRenderers)
                if (r != null)
                    highlighter.ToggleHighlight(true, r, AGeneralHighlighter.HighlightType.Control, useObstructedMaterial: true, forced: true);
            highlightedDumpster = dumpster;
        }

        private void ClearHighlight()
        {
            if (glowInstance != null)
                glowInstance.SetActive(false);

            if (outlineRenderers != null)
            {
                AGeneralHighlighter highlighter = SingletonBehaviour<AGeneralHighlighter>.Instance;
                if (highlighter != null)
                    foreach (Renderer r in outlineRenderers)
                        if (r != null)
                            highlighter.ToggleHighlight(false, r, AGeneralHighlighter.HighlightType.Control, useObstructedMaterial: true, forced: true);
                outlineRenderers = null;
            }

            highlightedDumpster = null;
        }

        // Clone the loco cab's glow object the first time we need it.
        private bool TryInitGlow()
        {
            if (glowInstance != null) return true;

            TeleportHoverGlow source = Object.FindObjectOfType<TeleportHoverGlow>();
            if (source == null || source.highlight == null) return false;

            glowInstance = Object.Instantiate(source.highlight);
            Object.DontDestroyOnLoad(glowInstance);
            glowInstance.transform.SetParent(null);
            foreach (Collider c in glowInstance.GetComponentsInChildren<Collider>(true))
                Object.Destroy(c); // never let the glow block our raycast

            glowBlock = new MaterialPropertyBlock();
            Renderer gr = glowInstance.GetComponentInChildren<Renderer>(includeInactive: true);
            if (gr != null && gr.sharedMaterial != null && gr.sharedMaterial.HasProperty(TINT_COLOR))
            {
                glowBaseColor = gr.sharedMaterial.GetColor(TINT_COLOR);
                glowBaseCaptured = true;
            }

            glowInstance.SetActive(false);
            return true;
        }

        // Brighten the cloned glow (it's tuned to be faint up close on locos).
        private void BoostGlow()
        {
            if (!glowBaseCaptured || glowBlock == null) return;

            float i = Main.Settings.glowIntensity;
            Color c = new Color(glowBaseColor.r * i, glowBaseColor.g * i, glowBaseColor.b * i, 1f);
            foreach (Renderer r in glowInstance.GetComponentsInChildren<Renderer>(includeInactive: true))
            {
                r.GetPropertyBlock(glowBlock);
                glowBlock.SetColor(TINT_COLOR, c);
                r.SetPropertyBlock(glowBlock);
            }
        }

        private void PositionAndScaleGlow(ItemDumpster dumpster)
        {
            Bounds b = GetBinBounds(dumpster);
            glowInstance.transform.position = b.center;
            glowInstance.transform.localScale = Vector3.one;

            Renderer gr = glowInstance.GetComponentInChildren<Renderer>(includeInactive: true);
            if (gr != null)
            {
                glowInstance.SetActive(true); // needed for valid bounds
                float natural = gr.bounds.size.magnitude;
                if (natural > 0.0001f)
                {
                    float s = b.size.magnitude / natural;
                    glowInstance.transform.localScale = new Vector3(s, s, s);
                }
            }
        }

        // The ItemDumpster usually sits on a child trigger with no renderer of its own,
        // so climb to the nearest ancestor that actually has the bin's meshes.
        private static Renderer[] GetBinRenderers(ItemDumpster dumpster)
        {
            Transform t = dumpster.transform;
            for (int level = 0; level < 4 && t != null; level++)
            {
                Renderer[] found = t.GetComponentsInChildren<Renderer>();
                if (found.Length > 0 && found.Length <= 40)
                    return found;
                t = t.parent;
            }
            return dumpster.GetComponentsInChildren<Renderer>();
        }

        private static Bounds GetBinBounds(ItemDumpster dumpster)
        {
            Renderer[] rs = GetBinRenderers(dumpster);
            if (rs != null && rs.Length > 0)
            {
                Bounds b = rs[0].bounds;
                for (int i = 1; i < rs.Length; i++)
                    b.Encapsulate(rs[i].bounds);
                return b;
            }

            foreach (Collider c in dumpster.GetComponentsInChildren<Collider>())
                if (c.isTrigger) return c.bounds;

            return new Bounds(dumpster.transform.position, Vector3.one * 0.5f);
        }

        // --- Tossing ------------------------------------------------------------

        private void TossItem(ItemDumpster dumpster, AGrabHandler held, ItemBase item)
        {
            // 1) Release it from the hand (re-enables physics, resets it to a world item).
            held.ForceEndInteraction();

            // 2) Place it just above the bin opening and let gravity drop it in.
            Vector3 dropPoint = AboveOpening(dumpster);
            Rigidbody rb = item.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.velocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.position = dropPoint;
            }
            item.transform.position = dropPoint;

            // 3) After it has visibly fallen in, dispose of it.
            //StartCoroutine(DisposeAfterFall(item, dumpster));

            Main.Log("Tossed " + item.name + " into the trash.");
        }

        private IEnumerator DisposeAfterFall(ItemBase item, ItemDumpster dumpster)
        {
            yield return new WaitForSeconds(FALL_TIME);
            if (item == null) yield break;

            // Job booklets are refused by the dumpster; remove them the proper way.
            JobBooklet booklet = item.GetComponent<JobBooklet>();
            if (booklet != null)
            {
                booklet.DestroyJobBooklet();
                yield break;
            }

            // Licenses are also refused; clean them up ourselves (keeps license ownership).
            if (Main.Settings.allowLicenses && IsLicenseItem(item))
            {
                CleanDestroy(item);
                yield break;
            }

            // Everything else: register with the dumpster (guarded; the trigger may have
            // already caught it as it fell — the dumpster ignores double-registration).
            if (RegisterMethod != null && item.GetComponent<RespawnOnDrop>() != null)
            {
                try { RegisterMethod.Invoke(dumpster, new object[] { item, false }); }
                catch { /* the OnTriggerEnter path is the fallback */ }
            }
        }

        // Mirrors JobBooklet.DestroyJobBooklet's cleanup for items without their own method.
        private static void CleanDestroy(ItemBase item)
        {
            Inventory inv = SingletonBehaviour<Inventory>.Instance;
            int idx = (inv != null) ? inv.IndexOf(item.gameObject) : -1;
            if (idx < 0)
                SingletonBehaviour<StorageController>.Instance?.RemoveItemFromStorageItemList(item);
            else
                inv.DropItemFromHandsOrInventory(idx);

            Object.Destroy(item.gameObject);
        }

        private static Vector3 AboveOpening(ItemDumpster dumpster)
        {
            Bounds b = GetBinBounds(dumpster);
            return new Vector3(b.center.x, b.max.y + DROP_HEIGHT_ABOVE_OPENING, b.center.z);
        }

        private void OnDestroy()
        {
            ClearHighlight();
            if (glowInstance != null)
                Object.Destroy(glowInstance);
        }
    }
}
