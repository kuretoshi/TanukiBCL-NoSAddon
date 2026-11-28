using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Il2CppInterop.Runtime.InteropTypes.Arrays;
using UnityEngine;
using Virial;

namespace TanukiBCL.Roles;

// Adds an ordinary short-task prefab and console after NoS finishes changing
// the map. Vanilla task assignment and NextStep/Complete retain task counting.
public sealed class VoiceAuthenticationMap : IDisposable
{
    private const string Name = "TanukiVoiceAuthentication";
    private const TaskTypes Kind = (TaskTypes)42420;
    private readonly ShipStatus ship;
    private GameObject? terminal;
    private NormalPlayerTask? template;
    private Material? material;
    private BoxCollider2D? footprint;
    private Collider2D? localCollider;
    private bool? collisionIgnored;
    private bool exitGrace;
    private sealed record PickerPlacement(Transform Parent, Vector3 Position);
    private static readonly Dictionary<int, PickerPlacement> pickerPositions = new();
    private static TaskAdderGame? activePicker;
    private static TaskFolder? displayedFolder;
    private static int displayedItemCount = -1;
    private static float nextPickerCheck;
    public global::Console? Console { get; private set; }
    public TaskTypes SourceKind { get; private set; }
    public VoiceAuthenticationMap(ShipStatus ship) { this.ship = ship; }
    public static bool IsTask(PlayerTask? task) => task != null && task.gameObject.name.StartsWith(Name, StringComparison.Ordinal);
    public bool Install()
    {
        var source = ship.ShortTasks.FirstOrDefault(task => task != null && !IsTask(task));
        var anchor = ship.AllConsoles.Where(c => c != null && c.gameObject.activeInHierarchy && c.Image != null && c.Image.sprite != null)
            .OrderBy(c => c.Room == SystemTypes.Comms ? 0 : 1).ThenBy(c => c.ConsoleId).FirstOrDefault();
        if (source == null || anchor == null) return false;
        var occupied = ship.CommonTasks.Concat(ship.ShortTasks).Concat(ship.LongTasks).Select(task => task.Index).ToHashSet();
        int index = Enumerable.Range(1, 254).Reverse().FirstOrDefault(id => !occupied.Contains(id));
        int consoleId = Enumerable.Range(1, 254).Reverse().FirstOrDefault(id => !ship.AllConsoles.Any(c => c.ConsoleId == id));
        if (index == 0 || consoleId == 0) return false;
        SourceKind = source.TaskType;
        terminal = new GameObject(Name + "Console");
        terminal.SetActive(false);
        terminal.layer = anchor.gameObject.layer;
        terminal.transform.SetParent(ship.transform, false);
        // Put the interaction point in front of the solid base, so the
        // console's wall check does not hit its own machine when used.
        const float usePointOffset = .56f;
        terminal.transform.position = anchor.transform.position + new Vector3(0f, -.65f - usePointOffset, -.01f);
        // The map console supplies only the placement, layer and outline
        // material. The device itself always uses our dedicated radio artwork.
        var machine = new GameObject("Machine");
        machine.layer = terminal.layer;
        machine.transform.SetParent(terminal.transform, false);
        var renderer = machine.AddComponent<SpriteRenderer>();
        renderer.sprite = VoiceAuthenticationTerminalVisual.Sprite;
        var center = renderer.sprite.bounds.center;
        machine.transform.localPosition = new Vector3(renderer.flipX ? center.x : -center.x,
            (renderer.flipY ? center.y : -center.y) + usePointOffset, 0f);
        renderer.sortingLayerID = anchor.Image.sortingLayerID;
        renderer.sortingOrder = anchor.Image.sortingOrder;
        float width = renderer.sprite.bounds.size.x;
        if (width > 0) terminal.transform.localScale = Vector3.one * (1f / width);
        if (anchor.Image.sharedMaterial != null)
        {
            material = new Material(anchor.Image.sharedMaterial);
            renderer.sharedMaterial = material;
        }
        var collider = terminal.AddComponent<CircleCollider2D>();
        collider.radius = width * .5f; collider.isTrigger = true;
        // The trigger is for interaction only. A separate Ship-layer body
        // blocks movement at the feet, like the map's other standing props.
        var body = new GameObject("SolidBase");
        body.layer = LayerMask.NameToLayer("Ship");
        body.transform.SetParent(terminal.transform, false);
        footprint = body.AddComponent<BoxCollider2D>();
        footprint.isTrigger = false;
        footprint.size = new Vector2(width * .74f, width * .24f);
        footprint.offset = new Vector2(width * -.04f, width * (usePointOffset - .32f));
        Console = terminal.AddComponent<global::Console>();
        Console.Image = renderer;
        Console.ConsoleId = consoleId;
        Console.Room = anchor.Room;
        Console.usableDistance = .9f;
        Console.checkWalls = true;
        Console.onlyFromBelow = false;
        Console.onlySameRoom = false;
        Console.GhostsIgnored = false;
        Console.AllowImpostor = false;
        Console.TaskTypes = new[] { Kind };
        Console.ValidTasks = new Il2CppReferenceArray<TaskSet>(0);
        terminal.SetActive(true);
        UpdateCollision();
        // A vanilla task component can share its object with the free-play
        // task-picker artwork. Cloning that object also clones its paper icon,
        // labels and buttons into the world. Build a data-only task instead.
        var taskObject = new GameObject(Name + "Task");
        taskObject.SetActive(false);
        taskObject.transform.SetParent(ship.transform, false);
        template = taskObject.AddComponent<NormalPlayerTask>();
        template.Index = index;
        template.TaskType = Kind;
        template.StartAt = Console.Room;
        template.Length = NormalPlayerTask.TaskLength.Short;
        template.MaxStep = VoiceAuthenticationTask.WordCount;
        template.ShowTaskStep = true;
        template.Data = new byte[0];
        template.MinigamePrefab = source.MinigamePrefab;
        // Keep only the task arrow prefab, never the source UI hierarchy.
        if (source.Arrow != null)
        {
            template.Arrow = UnityEngine.Object.Instantiate(source.Arrow, taskObject.transform);
            template.Arrow.gameObject.SetActive(false);
        }
        ship.ShortTasks = ship.ShortTasks.Append(template).ToArray();
        ship.AllConsoles = ship.AllConsoles.Append(Console).ToArray();
        NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Message("Data-only short task registered; index=" + index + "; console=" + consoleId + "; room=" + Console.Room + "; machineSprite=" + renderer.sprite.name);
        return true;
    }
    public bool Near(Virial.Compat.Vector2 position)
    {
        if (terminal == null) return false;
        var p = terminal.transform.position;
        return (p.x - position.x) * (p.x - position.x) + (p.y - position.y) * (p.y - position.y) <= 2.25f;
    }
    public void UpdateCollision()
    {
        if (footprint == null) return;
        var player = PlayerControl.LocalPlayer;
        if (player == null || player.Collider == null) return;
        if (localCollider != player.Collider)
        {
            if (localCollider != null) Physics2D.IgnoreCollision(footprint, localCollider, false);
            localCollider = player.Collider;
            collisionIgnored = null;
            // Enabling this task in freeplay must not trap a player already
            // standing inside the newly placed machine. Let them leave once.
            exitGrace = footprint.bounds.Intersects(localCollider.bounds);
        }
        if (exitGrace && !footprint.bounds.Intersects(localCollider.bounds)) exitGrace = false;
        bool ignore = exitGrace || player.Data?.IsDead == true;
        if (collisionIgnored == ignore) return;
        Physics2D.IgnoreCollision(footprint, localCollider, ignore);
        collisionIgnored = ignore;
    }
    public static void InstallPatches(Harmony harmony)
    {
        harmony.Patch(AccessTools.Method(typeof(TranslationController), nameof(TranslationController.GetString), new[] { typeof(TaskTypes) }),
            prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(TranslateTaskName)));
        harmony.Patch(AccessTools.Method(typeof(ShipStatus), nameof(ShipStatus.GetTaskById)), prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(ResolveTask)));
        harmony.Patch(AccessTools.Method(typeof(global::Console), nameof(global::Console.Use)), prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(Use)));
        harmony.Patch(AccessTools.Method(typeof(NormalPlayerTask), nameof(NormalPlayerTask.Initialize)),
            prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(BeforeInitialize)),
            postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(AfterInitialize)) { priority = Priority.Last });
        harmony.Patch(AccessTools.Method(typeof(NormalPlayerTask), nameof(NormalPlayerTask.ValidConsole)), prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(ValidConsole)));
        harmony.Patch(AccessTools.Method(typeof(NormalPlayerTask), nameof(NormalPlayerTask.AppendTaskText)), prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(TaskText)));
        harmony.Patch(AccessTools.Method(typeof(NormalPlayerTask), nameof(NormalPlayerTask.UpdateArrowAndLocation)), prefix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(Arrow)) { priority = Priority.Last });
        harmony.Patch(AccessTools.PropertyGetter(typeof(PlayerControl), nameof(PlayerControl.CanMove)), postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(CanMove)));
        harmony.Patch(AccessTools.Method(typeof(TaskAddButton), nameof(TaskAddButton.Update)), postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(TaskAdderLabel)));
        harmony.Patch(AccessTools.Method(typeof(TaskAdderGame), nameof(TaskAdderGame.AddFileAsChild)),
            postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(AfterAddFile)));
        harmony.Patch(AccessTools.Method(typeof(TaskAdderGame), nameof(TaskAdderGame.ShowFolder)),
            postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(AfterShowFolder)));
        harmony.Patch(AccessTools.Method(typeof(TaskFolder), nameof(TaskFolder.OnClick)),
            postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(AfterFolderClick)));
        harmony.Patch(AccessTools.Method(typeof(TaskAdderGame), "OnEnable"),
            postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(PickerEnabled)));
        harmony.Patch(AccessTools.Method(typeof(TaskAdderGame), "OnDisable"),
            postfix: new HarmonyMethod(typeof(VoiceAuthenticationMap), nameof(PickerDisabled)));
    }
    private static bool TranslateTaskName([HarmonyArgument(0)] TaskTypes task, ref string __result)
    {
        // Vanilla indexes a fixed task-name array; our custom ID is outside it.
        if (task != Kind) return true;
        __result = VoiceAuthenticationTask.Text("title");
        return false;
    }
    private static bool ResolveTask(ShipStatus __instance, [HarmonyArgument(0)] byte index, ref NormalPlayerTask __result)
    {
        var map = VoiceAuthenticationTask.Current?.Map;
        if (map == null || map.ship != __instance || map.template == null || map.template.Index != index) return true;
        __result = map.template;
        return false;
    }
    private static bool Use(global::Console __instance)
    {
        var round = VoiceAuthenticationTask.Current;
        if (round?.Map.Console != __instance) return true;
        if (PlayerControl.LocalPlayer == null) return false;
        __instance.CanUse(PlayerControl.LocalPlayer.Data, out bool canUse, out _);
        if (canUse) round.Open();
        return false;
    }
    private static void BeforeInitialize(NormalPlayerTask __instance)
    {
        if (IsTask(__instance) && VoiceAuthenticationTask.Current != null) __instance.TaskType = VoiceAuthenticationTask.Current.Map.SourceKind;
    }
    private static void AfterInitialize(NormalPlayerTask __instance)
    {
        if (!IsTask(__instance)) return;
        var console = VoiceAuthenticationTask.Current?.Map.Console;
        if (console == null) return;
        __instance.TaskType = Kind;
        __instance.MaxStep = VoiceAuthenticationTask.WordCount;
        __instance.ShowTaskStep = true;
        __instance.ShowTaskTimer = false;
        __instance.useMultipleText = false;
        __instance.Data = new byte[] { (byte)console.ConsoleId };
        __instance.StartAt = console.Room;
        __instance.HasLocation = true;
        __instance.LocationDirty = true;
        if (__instance.Owner != null) __instance.gameObject.SetActive(true);
        __instance.UpdateArrowAndLocation();
    }
    private static bool ValidConsole(NormalPlayerTask __instance, [HarmonyArgument(0)] global::Console console, ref bool __result)
    {
        if (!IsTask(__instance)) return true;
        __result = !__instance.IsComplete && VoiceAuthenticationTask.Current?.Map.Console == console;
        return false;
    }
    private static bool TaskText(NormalPlayerTask __instance, [HarmonyArgument(0)] Il2CppSystem.Text.StringBuilder text)
    {
        if (!IsTask(__instance)) return true;
        string color = __instance.IsComplete ? "#55EE88" : "#FFDD66";
        text.Append("<color=" + color + ">" + VoiceAuthenticationTask.Text("title") + " (" + __instance.taskStep + "/" + __instance.MaxStep + ")</color>\n");
        return false;
    }
    private static bool Arrow(NormalPlayerTask __instance, bool __runOriginal)
    {
        if (!IsTask(__instance)) return true;
        var console = VoiceAuthenticationTask.Current?.Map.Console;
        if (console != null)
        {
            __instance.StartAt = console.Room;
            if (__instance.Arrow != null)
            {
                __instance.Arrow.target = console.transform.position;
                if (__runOriginal) __instance.Arrow.gameObject.SetActive(!__instance.IsComplete);
            }
        }
        return false;
    }
    private static void CanMove(PlayerControl __instance, ref bool __result)
    {
        if (__instance == PlayerControl.LocalPlayer && VoiceAuthenticationTask.Current?.WindowOpen == true) __result = false;
    }
    private static void TaskAdderLabel(TaskAddButton __instance)
    {
        if (!IsTask(__instance.MyTask)) return;
        if (__instance.Text != null) __instance.Text.text = VoiceAuthenticationTask.Text("title");
        if (pickerPositions.TryGetValue(__instance.GetInstanceID(), out var placement) && placement.Parent != null)
            PlacePickerFile(__instance, placement);
        else
            __instance.gameObject.SetActive(false);
    }
    private static void AfterAddFile(TaskAdderGame __instance, [HarmonyArgument(1)] TaskAddButton item)
    {
        // MyTask can be assigned AFTER AddFileAsChild returns. Save the native
        // slot for every file and identify our task after ShowFolder completes.
        pickerPositions[item.GetInstanceID()] = new(__instance.TaskParent,
            __instance.TaskParent.InverseTransformPoint(item.transform.position));
    }
    private static void PickerEnabled(TaskAdderGame __instance)
    {
        activePicker = __instance;
        displayedFolder = null;
        displayedItemCount = -1;
        nextPickerCheck = 0f;
    }
    private static void PickerDisabled(TaskAdderGame __instance)
    {
        if (activePicker != __instance) return;
        activePicker = null;
        displayedFolder = null;
        displayedItemCount = -1;
        pickerPositions.Clear();
    }
    private static void AfterFolderClick(TaskFolder __instance)
    {
        if (__instance.Parent != null) AfterShowFolder(__instance.Parent, __instance);
    }
    public static void UpdatePicker()
    {
        // Native calls between IL2CPP methods may be inlined and bypass a
        // Harmony ShowFolder hook. The breadcrumb is the actual current page.
        if (Time.unscaledTime < nextPickerCheck) return;
        nextPickerCheck = Time.unscaledTime + .1f;
        if (activePicker == null || !activePicker.gameObject.activeInHierarchy || activePicker.Hierarchy.Count == 0) return;
        var folder = activePicker.Hierarchy[activePicker.Hierarchy.Count - 1];
        if (folder != null && (folder != displayedFolder || displayedItemCount != activePicker.ActiveItems.Count))
            AfterShowFolder(activePicker, folder);
    }
    private static void PlacePickerFile(TaskAddButton item, PickerPlacement placement)
    {
        item.transform.SetParent(placement.Parent, false);
        item.transform.localPosition = placement.Position;
        item.SafePositionWorld = item.transform.position;
    }
    private static void AfterShowFolder(TaskAdderGame __instance, [HarmonyArgument(0)] TaskFolder taskFolder)
    {
        var candidates = UnityEngine.Object.FindObjectsOfType<TaskAddButton>(true)
            .Where(b => b != null && IsTask(b.MyTask)).ToArray();
        activePicker = __instance;
        displayedFolder = taskFolder;
        displayedItemCount = __instance.ActiveItems.Count;
        bool visible = taskFolder.TaskChildren.ToArray().Any(t => IsTask(t));
        NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Message(
            "Picker folder opened; folder=" + taskFolder.FolderName + "; containsVoiceTask=" + visible +
            "; voiceButtons=" + candidates.Length + "; activeItems=" + __instance.ActiveItems.Count);
        foreach (var button in candidates) button.gameObject.SetActive(false);
        if (!visible) return;
        var normal = __instance.ActiveItems.ToArray()
            .Select(t => t.GetComponent<TaskAddButton>()).Where(b => b != null && !IsTask(b.MyTask)).ToArray();
        var item = candidates.FirstOrDefault(b => __instance.ActiveItems.Contains(b.transform)) ?? candidates.FirstOrDefault();
        if (item == null)
        {
            item = UnityEngine.Object.Instantiate(__instance.TaskPrefab, __instance.TaskParent);
            item.MyTask = VoiceAuthenticationTask.Current?.Map.template;
        }
        bool nativeSlot = pickerPositions.TryGetValue(item.GetInstanceID(), out var placement);
        if (!nativeSlot)
        {
            // Also cover builds where IL2CPP inlines AddFileAsChild. Derive the
            // next slot from the ordinary files already laid out on this page.
            var points = normal.Select(b => __instance.TaskParent.InverseTransformPoint(b.transform.position)).ToArray();
            var last = points.LastOrDefault();
            float step = __instance.fileWidth;
            for (int i = 1; i < points.Length; i++)
                if (Mathf.Abs(points[i].y - points[i - 1].y) < .01f && points[i].x > points[i - 1].x)
                    step = points[i].x - points[i - 1].x;
            var next = last + new Vector3(step, 0f, 0f);
            float left = points.Length > 0 ? points.Min(p => p.x) : -__instance.lineWidth * .5f;
            if (next.x - left + step > __instance.lineWidth)
            {
                float height = 0f;
                foreach (var button in normal)
                {
                    if (button.FileImage != null && button.Text != null)
                    {
                        var top = __instance.TaskParent.InverseTransformPoint(button.FileImage.bounds.max);
                        var bottom = __instance.TaskParent.InverseTransformPoint(button.Text.bounds.min);
                        height = Mathf.Max(height, top.y - bottom.y);
                    }
                }
                next = new Vector3(left, last.y - Mathf.Max(step, height), last.z);
            }
            placement = new(__instance.TaskParent, next);
        }
        if (normal.Length > 0)
        {
            item.transform.localScale = normal[0].transform.localScale;
            if (item.Text != null && normal[0].Text != null) item.Text.fontSize = normal[0].Text.fontSize;
        }
        pickerPositions[item.GetInstanceID()] = placement!;
        PlacePickerFile(item, placement!);
        if (!__instance.ActiveItems.Contains(item.transform)) __instance.ActiveItems.Add(item.transform);
        displayedItemCount = __instance.ActiveItems.Count;
        if (item.Button != null && !__instance.ControllerSelectable.Contains(item.Button)) __instance.ControllerSelectable.Add(item.Button);
        item.gameObject.SetActive(true);
        if (item.Text != null) item.Text.text = VoiceAuthenticationTask.Text("title");
        NebulaAPI.Logging.NebulaLogger("TanukiVoiceAuthentication").Message(
            "Picker finalized; folder=" + taskFolder.FolderName + "; normalFiles=" + normal.Length +
            "; nativeSlot=" + nativeSlot + "; local=" + placement!.Position + "; parent=" + __instance.TaskParent.name);
    }
    public void Dispose()
    {
        if (footprint != null) footprint.enabled = false;
        if (ship != null)
        {
            if (Console != null) ship.AllConsoles = ship.AllConsoles.Where(c => c != Console).ToArray();
            if (template != null) ship.ShortTasks = ship.ShortTasks.Where(t => t != template).ToArray();
        }
        if (terminal != null) UnityEngine.Object.Destroy(terminal);
        if (template != null) UnityEngine.Object.Destroy(template.gameObject);
        if (material != null) UnityEngine.Object.Destroy(material);
        terminal = null; template = null; Console = null; material = null;
        footprint = null; localCollider = null; collisionIgnored = null;
        pickerPositions.Clear();
        activePicker = null; displayedFolder = null; displayedItemCount = -1;
    }
}
