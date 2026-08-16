#if UNITY_EDITOR

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;
using VRC.SDKBase;

namespace MagicChatbox.Avatar.Editor
{
    /// <summary>
    /// Generates the control assets rather than shipping them.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Shipping a prebuilt AnimatorController, parameters asset and menu means shipping serialized
    /// Unity YAML that is pinned to whichever editor and SDK version produced it. Generating them in
    /// the project instead means Unity writes them, so they are correct for the version the creator
    /// actually has, and a broken result is a compiler error rather than a corrupt asset that
    /// imports quietly and does nothing.
    /// </para>
    /// <para>
    /// The parameter names below are the Control tier of the app's published contract. There is a
    /// test in the desktop repository that reads this file and fails if the two lists drift apart, so
    /// treat <see cref="Controls"/> as the copy that must be kept in step, not as a free-form list.
    /// </para>
    /// </remarks>
    public static class MagicChatboxAvatarSetup
    {
        private const string OutputFolder = "Assets/MagicChatbox";
        private const string MenuName = "MagicChatbox";

        /// <summary>One entry per inbound control. Impulses only: every one of these stops something.</summary>
        private static readonly (string Parameter, string Label)[] Controls =
        {
            ("MCB/Ctrl/Tts/Stop", "Stop speaking"),
            ("MCB/Ctrl/Panic", "Stop everything"),
        };

        [MenuItem("Tools/MagicChatbox/Generate avatar controls")]
        public static void Generate()
        {
            Directory.CreateDirectory(OutputFolder);

            AnimatorController controller = CreateController();
            VRCExpressionParameters parameters = CreateParameters();
            VRCExpressionsMenu menu = CreateMenu();

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Selection.activeObject = controller;

            Debug.Log(
                $"MagicChatbox: generated {Controls.Length} control(s) into {OutputFolder}. " +
                "Merge the controller, parameters and menu onto your avatar with VRCFury or Modular Avatar. " +
                "Every parameter is unsynced, so this costs no synced parameter bits.",
                parameters);

            if (menu == null)
            {
                Debug.LogError("MagicChatbox: the menu asset was not created.");
            }
        }

        private static AnimatorController CreateController()
        {
            string path = $"{OutputFolder}/MagicChatboxFX.controller";
            AssetDatabase.DeleteAsset(path);

            AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(path);

            // An empty clip keeps every state a legal state without touching a single transform on
            // the avatar. Nothing here animates anything.
            var idle = new AnimationClip { name = "MagicChatboxIdle" };
            AssetDatabase.AddObjectToAsset(idle, controller);

            foreach ((string parameter, string label) in Controls)
            {
                controller.AddParameter(parameter, AnimatorControllerParameterType.Bool);

                AnimatorControllerLayer layer = NewLayer(controller, label);
                AnimatorStateMachine machine = layer.stateMachine;

                AnimatorState waiting = machine.AddState("Waiting");
                waiting.motion = idle;
                waiting.writeDefaultValues = false;

                AnimatorState pressed = machine.AddState("Pressed");
                pressed.motion = idle;
                pressed.writeDefaultValues = false;

                // Set, never Add and never Random: VRChat documents both of those as unreliable on
                // remote instances, and this driver is what puts the parameter back to false so the
                // next press is a fresh edge even when the desktop app is not running.
                var driver = pressed.AddStateMachineBehaviour<VRCAvatarParameterDriver>();
                driver.parameters = new List<VRC_AvatarParameterDriver.Parameter>
                {
                    new VRC_AvatarParameterDriver.Parameter
                    {
                        type = VRC_AvatarParameterDriver.ChangeType.Set,
                        name = parameter,
                        value = 0f,
                    },
                };

                machine.defaultState = waiting;

                AnimatorStateTransition press = waiting.AddTransition(pressed);
                press.hasExitTime = false;
                press.duration = 0f;
                press.AddCondition(AnimatorConditionMode.If, 0f, parameter);

                AnimatorStateTransition release = pressed.AddTransition(waiting);
                release.hasExitTime = false;
                release.duration = 0f;
                release.AddCondition(AnimatorConditionMode.IfNot, 0f, parameter);
            }

            EditorUtility.SetDirty(controller);
            return controller;
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
            string path = $"{OutputFolder}/MagicChatboxParameters.asset";
            AssetDatabase.DeleteAsset(path);

            var asset = ScriptableObject.CreateInstance<VRCExpressionParameters>();
            var entries = new VRCExpressionParameters.Parameter[Controls.Length];

            for (int i = 0; i < Controls.Length; i++)
            {
                entries[i] = new VRCExpressionParameters.Parameter
                {
                    name = Controls[i].Parameter,
                    valueType = VRCExpressionParameters.ValueType.Bool,
                    defaultValue = 0f,
                    saved = false,

                    // The whole point: unsynced costs nothing against the 256 bit budget, and these
                    // are driven locally by OSC rather than replicated to remote players.
                    networkSynced = false,
                };
            }

            asset.parameters = entries;

            AssetDatabase.CreateAsset(asset, path);
            return asset;
        }

        private static VRCExpressionsMenu CreateMenu()
        {
            string path = $"{OutputFolder}/MagicChatboxMenu.asset";
            AssetDatabase.DeleteAsset(path);

            var menu = ScriptableObject.CreateInstance<VRCExpressionsMenu>();
            menu.name = MenuName;
            menu.controls = new List<VRCExpressionsMenu.Control>();

            // VRChat caps a menu at eight controls. Two is not near it, but the guard stays so that
            // adding controls later fails here rather than in the SDK validator.
            int count = Mathf.Min(Controls.Length, 8);

            for (int i = 0; i < count; i++)
            {
                menu.controls.Add(new VRCExpressionsMenu.Control
                {
                    name = Controls[i].Label,
                    type = VRCExpressionsMenu.Control.ControlType.Button,
                    parameter = new VRCExpressionsMenu.Control.Parameter { name = Controls[i].Parameter },
                    value = 1f,
                });
            }

            if (Controls.Length > 8)
            {
                Debug.LogWarning("MagicChatbox: more controls than a VRChat menu page holds; the rest were skipped.");
            }

            AssetDatabase.CreateAsset(menu, path);
            return menu;
        }
    }
}

#endif
