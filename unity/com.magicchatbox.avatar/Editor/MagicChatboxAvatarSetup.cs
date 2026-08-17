#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace MagicChatbox.Avatar.Editor
{
    /// <summary>One inbound control: a parameter, how it is presented, and whether it persists.</summary>
    public readonly struct MagicChatboxControl
    {
        public MagicChatboxControl(
            string parameter,
            string label,
            VRCExpressionParameters.ValueType valueType,
            bool saved,
            VRCExpressionsMenu.Control.ControlType controlType,
            bool defaultOn = false)
        {
            Parameter = parameter;
            Label = label;
            ValueType = valueType;
            Saved = saved;
            ControlType = controlType;
            DefaultOn = defaultOn;
        }

        public string Parameter { get; }
        public string Label { get; }
        public VRCExpressionParameters.ValueType ValueType { get; }

        /// <summary>Whether VRChat should remember it. Wrong for a panic press, right for a preference.</summary>
        public bool Saved { get; }

        public VRCExpressionsMenu.Control.ControlType ControlType { get; }

        /// <summary>
        /// The value a freshly generated avatar starts with.
        /// </summary>
        /// <remarks>
        /// Load-bearing for the Config tier. Those parameters mean "this feature may run", and the app
        /// acts on one held off, so leaving them at Unity's default of 0 would mean generating a prefab
        /// that silently switches five features off for whoever wears it. A button is a different
        /// shape - it fires on the rising edge and starting at 0 is what makes the first press count.
        /// </remarks>
        public bool DefaultOn { get; }
    }

    /// <summary>
    /// Generates the control assets rather than shipping them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shipping a prebuilt AnimatorController, parameters asset and menu means shipping serialized
    /// Unity YAML pinned to whichever editor and SDK version produced it. Generating them in the
    /// project means Unity writes them, so they are correct for the version the creator actually has,
    /// and a broken result is a compiler error rather than a corrupt asset that imports quietly and
    /// does nothing.
    /// </para>
    /// <para>
    /// The control list below is the Control tier of the app's published contract. A test in the
    /// desktop repository reads this file and fails if the two drift apart.
    /// </para>
    /// </remarks>
    public static class MagicChatboxAvatarSetup
    {
        /// <summary>Bumped when the wire contract changes. Encoded in a parameter NAME, never a value.</summary>
        /// <remarks>
        /// VRChat's OSCQuery reports stale values for parameters that have not changed since load, so a
        /// version int is unreadable from outside. A bool whose existence is the signal is not. VRCFury
        /// encodes its own version the same way and for the same reason.
        /// </remarks>
        public const int ContractVersion = 1;

        public const string VersionParameter = "MCB/Version/1";

        private const string OutputFolder = "Assets/MagicChatbox";
        private const string MenuName = "MagicChatbox";

        /// <summary>VRChat's documented cap on controls in one menu page.</summary>
        private const int MenuPageSize = 8;

        public static readonly MagicChatboxControl[] Controls =
        {
            new MagicChatboxControl(
                "MCB/Ctrl/Tts/Stop", "Stop speaking",
                VRCExpressionParameters.ValueType.Bool, false,
                VRCExpressionsMenu.Control.ControlType.Button),

            new MagicChatboxControl(
                "MCB/Ctrl/Panic", "Stop everything",
                VRCExpressionParameters.ValueType.Bool, false,
                VRCExpressionsMenu.Control.ControlType.Button),

            // Level rather than edge: these follow the switch, so an avatar that remembers one puts
            // you back where you left off rather than waiting for a press that never comes.
            new MagicChatboxControl(
                "MCB/Ctrl/Afk", "Away",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle),

            new MagicChatboxControl(
                "MCB/Ctrl/Status/Cycle", "Cycle my status",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle),

            // The Config tier. These are toggles rather than buttons, and saved rather than
            // momentary, because the whole point is that the avatar remembers them: wear the avatar
            // and the feature stays off for as long as you are wearing it.
            //
            // They default to ON so a freshly generated avatar changes nothing. The app refuses to
            // act on one held on, so an untouched control is inert by construction rather than by
            // luck - a creator who ships this prefab cannot accidentally switch anything off for
            // somebody else.
            new MagicChatboxControl(
                "MCB/Cfg/Sending", "MagicChatbox on",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle, true),

            new MagicChatboxControl(
                "MCB/Cfg/HeartRate", "Show heart rate",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle, true),

            new MagicChatboxControl(
                "MCB/Cfg/Media", "Show what I am playing",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle, true),

            new MagicChatboxControl(
                "MCB/Cfg/WindowActivity", "Show my open app",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle, true),

            new MagicChatboxControl(
                "MCB/Cfg/Status", "Show my status",
                VRCExpressionParameters.ValueType.Bool, true,
                VRCExpressionsMenu.Control.ControlType.Toggle, true),
        };

        [MenuItem("Tools/MagicChatbox/Generate avatar controls")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);

            bool writeDefaults = DetectWriteDefaults();

            AnimatorController controller = CreateController(writeDefaults);
            VRCExpressionParameters parameters = CreateParameters();
            VRCExpressionsMenu menu = CreateMenu();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = controller;

            Debug.Log(
                $"MagicChatbox: generated {Controls.Length} control(s) into {OutputFolder}, " +
                $"Write Defaults {(writeDefaults ? "on" : "off")} to match the avatar. " +
                "Merge the controller, parameters and menu with VRCFury or Modular Avatar. " +
                "Every parameter is unsynced, so this costs no synced parameter bits.",
                parameters);

            if (menu == null)
            {
                Debug.LogError("MagicChatbox: the menu asset was not created.");
            }
        }

        /// <summary>
        /// Matches the avatar's own Write Defaults rather than imposing ours.
        /// </summary>
        /// <remarks>
        /// A mismatch produces an SDK warning the creator cannot attribute to us, and states with
        /// Write Defaults off whose clip is empty produce a second one. Matched on layer type rather
        /// than index because the layer order is not fixed.
        /// </remarks>
        private static bool DetectWriteDefaults()
        {
            VRCAvatarDescriptor descriptor = Selection.activeGameObject != null
                ? Selection.activeGameObject.GetComponentInParent<VRCAvatarDescriptor>()
                : Object.FindObjectOfType<VRCAvatarDescriptor>();

            if (descriptor == null)
            {
                return true;
            }

            foreach (VRCAvatarDescriptor.CustomAnimLayer layer in descriptor.baseAnimationLayers)
            {
                if (layer.type != VRCAvatarDescriptor.AnimLayerType.FX)
                {
                    continue;
                }

                if (layer.animatorController is AnimatorController fx)
                {
                    return DominantWriteDefaults(fx);
                }
            }

            return true;
        }

        private static bool DominantWriteDefaults(AnimatorController controller)
        {
            int on = 0;
            int off = 0;

            foreach (AnimatorControllerLayer layer in controller.layers)
            {
                if (layer.stateMachine == null)
                {
                    continue;
                }

                foreach (ChildAnimatorState child in layer.stateMachine.states)
                {
                    if (child.state.writeDefaultValues)
                    {
                        on++;
                    }
                    else
                    {
                        off++;
                    }
                }
            }

            return on >= off;
        }

        private static AnimatorController CreateController(bool writeDefaults)
        {
            string path = OutputFolder + "/MagicChatboxFX.controller";
            AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            AnimationClip idle = CreateIdleClip(writeDefaults);
            AssetDatabase.AddObjectToAsset(idle, controller);

            foreach (MagicChatboxControl control in Controls)
            {
                controller.AddParameter(control.Parameter, AnimatorControllerParameterType.Bool);

                AnimatorControllerLayer layer = NewLayer(controller, control.Label);
                AnimatorStateMachine machine = layer.stateMachine;

                AnimatorState waiting = machine.AddState("Waiting");
                waiting.motion = idle;
                waiting.writeDefaultValues = writeDefaults;

                AnimatorState pressed = machine.AddState("Pressed");
                pressed.motion = idle;
                pressed.writeDefaultValues = writeDefaults;

                // Set, never Add and never Random: VRChat documents both as unreliable on remote
                // instances, and this driver is what returns the parameter to false so the next press
                // is a fresh edge even when the desktop app is not running.
                VRCAvatarParameterDriver driver = pressed.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
                {
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = control.Parameter,
                        value = 0f,
                    },
                };

                machine.defaultState = waiting;

                AnimatorStateTransition press = waiting.AddTransition(pressed);
                press.hasExitTime = false;
                press.duration = 0f;
                press.AddCondition(AnimatorConditionMode.If, 0f, control.Parameter);

                AnimatorStateTransition release = pressed.AddTransition(waiting);
                release.hasExitTime = false;
                release.duration = 0f;
                release.AddCondition(AnimatorConditionMode.IfNot, 0f, control.Parameter);
            }

            EditorUtility.SetDirty(controller);
            return controller;
        }

        /// <summary>
        /// An idle clip that animates nothing on the avatar.
        /// </summary>
        /// <remarks>
        /// When Write Defaults is off the clip must not be empty, or the SDK reports "animator states
        /// with Write Defaults disabled where the animation clip is either missing or empty". One
        /// curve on the installer object's own scale satisfies it without touching the avatar.
        /// </remarks>
        private static AnimationClip CreateIdleClip(bool writeDefaults)
        {
            var clip = new AnimationClip { name = "MagicChatboxIdle" };

            if (!writeDefaults)
            {
                clip.SetCurve(
                    "MagicChatbox",
                    typeof(Transform),
                    "m_LocalScale.x",
                    AnimationCurve.Constant(0f, 1f / 60f, 1f));
            }

            return clip;
        }

        private static AnimatorControllerLayer NewLayer(AnimatorController controller, string name)
        {
            controller.AddLayer(name);

            AnimatorControllerLayer[] layers = controller.layers;
            AnimatorControllerLayer layer = layers[layers.Length - 1];
            layer.defaultWeight = 1f;
            controller.layers = layers;

            return controller.layers[controller.layers.Length - 1];
        }

        private static VRCExpressionParameters CreateParameters()
        {
            string path = OutputFolder + "/MagicChatboxParameters.asset";
            AssetDatabase.DeleteAsset(path);

            var asset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var entries = new List<VRCExpressionParameters.Parameter>();

            foreach (MagicChatboxControl control in Controls)
            {
                entries.Add(new VRCExpressionParameters.Parameter
                {
                    name = control.Parameter,
                    valueType = control.ValueType,
                    defaultValue = control.DefaultOn ? 1f : 0f,
                    saved = control.Saved,

                    // The whole point: unsynced costs nothing against the 256 bit budget, and these
                    // are driven locally over OSC rather than replicated to remote players.
                    networkSynced = false,
                });
            }

            // Presence is the handshake. The app looks for this name existing, never for its value.
            entries.Add(new VRCExpressionParameters.Parameter
            {
                name = VersionParameter,
                valueType = VRCExpressionParameters.ValueType.Bool,
                defaultValue = 0f,
                saved = false,
                networkSynced = false,
            });

            asset.parameters = entries.ToArray();

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static VRCExpressionsMenu CreateMenu()
        {
            string path = OutputFolder + "/MagicChatboxMenu.asset";
            AssetDatabase.DeleteAsset(path);

            VRCExpressionsMenu root = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            root.name = MenuName;
            root.controls = new List<VRCExpressionsMenu.Control>();

            AssetDatabase.CreateAsset(root, path);

            VRCExpressionsMenu page = root;
            int onPage = 0;
            int pageNumber = 1;

            foreach (MagicChatboxControl control in Controls)
            {
                // Both installers paginate on their own and would each pick a different arrangement.
                // Doing it here means VRCFury and Modular Avatar produce the same menu.
                if (onPage == MenuPageSize - 1 && control.Parameter != Controls.Last().Parameter)
                {
                    VRCExpressionsMenu next = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
                    pageNumber++;
                    next.name = MenuName + " (Page " + pageNumber + ")";
                    next.controls = new List<VRCExpressionsMenu.Control>();
                    AssetDatabase.AddObjectToAsset(next, root);

                    page.controls.Add(new VRCExpressionsMenu.Control
                    {
                        name = "More",
                        type = VRCExpressionsMenu.Control.ControlType.SubMenu,
                        subMenu = next,
                    });

                    page = next;
                    onPage = 0;
                }

                page.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = control.Label,
                    type = control.ControlType,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = control.Parameter },
                    value = 1f,
                });

                onPage++;
            }

            EditorUtility.SetDirty(root);
            return root;
        }
    }
}

#endif
