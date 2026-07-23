using UnityEngine;
using UnityEngine.InputSystem;

// Reassignation des touches. Les actions listees ici sont les boutons simples
// (un seul binding, non composite, clavier) : les composites (Move) et les
// actions souris/axe (LeftClick, RightClick, CameraAxis, ZoomAxis) restent
// hors-scope, une UI de reassignation par sous-binding etant plus complexe.
// Persistance via PlayerPrefs (JSON natif de l'Input System), au meme titre
// que GameSettings pour le reste des reglages.
public static class KeybindManager
{
    private const string PrefsKey = "Settings_KeybindOverrides";

    public struct RebindableAction
    {
        public string ActionName;
        public string Label;

        public RebindableAction(string actionName, string label)
        {
            ActionName = actionName;
            Label = label;
        }
    }

    public static readonly RebindableAction[] Rebindables =
    {
        new RebindableAction("Jump", "Saut"),
        new RebindableAction("SwimUp", "Nager - Monter"),
        new RebindableAction("SwimDown", "Nager - Descendre"),
        new RebindableAction("Attack", "Attaque"),
        new RebindableAction("NextTarget", "Cible suivante"),
        new RebindableAction("TargetSelf", "Se cibler soi-même"),
        new RebindableAction("Sit", "S'asseoir"),
        new RebindableAction("Inventory", "Inventaire"),
        new RebindableAction("CharacterStatus", "Statut du personnage"),
        new RebindableAction("Actions", "Actions"),
        new RebindableAction("CloseWindow", "Fermer une fenêtre"),
        new RebindableAction("SystemMenu", "Menu système"),
        new RebindableAction("Validate", "Valider"),
    };

    public static void ApplySavedOverrides(InputActionAsset actions)
    {
        string json = PlayerPrefs.GetString(PrefsKey, "");
        if (!string.IsNullOrEmpty(json))
        {
            actions.LoadBindingOverridesFromJson(json);
        }
    }

    public static string CaptureOverridesJson(InputActionAsset actions)
    {
        return actions.SaveBindingOverridesAsJson();
    }

    public static void SaveOverrides(InputActionAsset actions)
    {
        PlayerPrefs.SetString(PrefsKey, actions.SaveBindingOverridesAsJson());
        PlayerPrefs.Save();
    }

    public static void RestoreOverrides(InputActionAsset actions, string json)
    {
        actions.RemoveAllBindingOverrides();
        if (!string.IsNullOrEmpty(json))
        {
            actions.LoadBindingOverridesFromJson(json);
        }
        SaveOverrides(actions);
    }

    public static void ResetAll(InputActionAsset actions)
    {
        actions.RemoveAllBindingOverrides();
        PlayerPrefs.DeleteKey(PrefsKey);
        PlayerPrefs.Save();
    }

    public static string GetBindingDisplayString(InputAction action)
    {
        return action.GetBindingDisplayString(0);
    }

    // L'operation doit rester vivante tant qu'on ecoute une touche : le
    // membre est expose pour que l'appelant puisse l'annuler si besoin (ex. la
    // fenetre se ferme pendant qu'on ecoute).
    public static InputActionRebindingExtensions.RebindingOperation StartRebind(
        InputAction action,
        System.Action onComplete,
        System.Action onCancel)
    {
        action.Disable();

        InputActionRebindingExtensions.RebindingOperation operation = null;
        operation = action.PerformInteractiveRebinding(0)
            .WithControlsExcluding("Mouse")
            .WithCancelingThrough("<Keyboard>/escape")
            .OnComplete(op =>
            {
                op.Dispose();
                action.Enable();
                onComplete?.Invoke();
            })
            .OnCancel(op =>
            {
                op.Dispose();
                action.Enable();
                onCancel?.Invoke();
            });
        operation.Start();
        return operation;
    }
}
