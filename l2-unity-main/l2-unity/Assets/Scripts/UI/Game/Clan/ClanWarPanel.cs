using System.Collections.Generic;
using UnityEngine.UIElements;

// Onglet Guerres de la fenetre de clan : declarer la guerre a un clan par son
// nom, et arreter ou capituler une guerre choisie dans la liste. Le serveur
// verifie les conditions (niveau, effectifs, alliance, delai) et les annonce.
public class ClanWarPanel
{
    private readonly TextField _target;
    private readonly ScrollView _declared;
    private readonly ScrollView _attackers;
    private readonly Button _declareButton;
    private readonly Button _stopButton;
    private readonly Button _surrenderButton;

    private string _selected = string.Empty;

    public ClanWarPanel(VisualElement page)
    {
        _target = page.Q<VisualElement>("WarTarget").Q<TextField>("L2Input");
        _target.maxLength = 16;
        _declared = page.Q<ScrollView>("DeclaredList");
        _attackers = page.Q<ScrollView>("AttackerList");
        _declareButton = page.Q<Button>("DeclareWarBtn");
        _stopButton = page.Q<Button>("StopWarBtn");
        _surrenderButton = page.Q<Button>("SurrenderBtn");

        // Sans cela, taper un nom de clan deplacerait le personnage.
        _target.RegisterCallback<FocusEvent>(evt => L2GameUI.Instance.IsTyping = true);
        _target.RegisterCallback<BlurEvent>(evt => L2GameUI.Instance.IsTyping = false);

        RegisterButton(_declareButton, Declare);
        RegisterButton(_stopButton, () => ConfirmOnSelected("Arr\u00eater la guerre contre ", name => GameClient.Instance.ClientPacketHandler.SendStopPledgeWar(name)));
        RegisterButton(_surrenderButton, () => ConfirmOnSelected("Capituler face \u00e0 ", name => GameClient.Instance.ClientPacketHandler.SendSurrenderPledgeWar(name)));
    }

    private static void RegisterButton(Button button, System.Action action)
    {
        button.AddManipulator(new ButtonClickSoundManipulator(button));
        button.RegisterCallback<MouseUpEvent>(evt => action(), TrickleDown.TrickleDown);
    }

    public void Show()
    {
        _selected = string.Empty;
        ClanWars.Request();
        Refresh();
    }

    private void Declare()
    {
        string name = _target.value != null ? _target.value.Trim() : string.Empty;
        if (name.Length == 0)
        {
            return;
        }

        L2ConfirmWindow.Instance.ShowWindow(
            "D\u00e9clarer la guerre au clan " + name + " ?",
            () =>
            {
                GameClient.Instance.ClientPacketHandler.SendStartPledgeWar(name);
                _target.value = string.Empty;
            },
            () => { });
    }

    private void ConfirmOnSelected(string question, System.Action<string> action)
    {
        if (string.IsNullOrEmpty(_selected))
        {
            return;
        }

        string name = _selected;
        L2ConfirmWindow.Instance.ShowWindow(question + name + " ?", () => action(name), () => { });
    }

    public void Refresh()
    {
        Fill(_declared, ClanWars.Declared, true);
        Fill(_attackers, ClanWars.Attackers, false);

        bool hasSelection = !string.IsNullOrEmpty(_selected);
        _stopButton.SetEnabled(hasSelection);
        _surrenderButton.SetEnabled(hasSelection);
        _declareButton.SetEnabled(ClanData.HasClan);
    }

    private void Fill(ScrollView list, IReadOnlyList<string> clans, bool selectable)
    {
        list.Clear();

        foreach (string clan in clans)
        {
            VisualElement row = new VisualElement();
            row.AddToClassList("clan-member");
            row.AddToClassList("online");
            row.EnableInClassList("selected", selectable && clan == _selected);

            Label label = new Label(clan);
            label.AddToClassList("clan-col-name");
            label.pickingMode = PickingMode.Ignore;
            row.Add(label);

            if (selectable)
            {
                string name = clan;
                row.RegisterCallback<MouseUpEvent>(evt =>
                {
                    _selected = _selected == name ? string.Empty : name;
                    Refresh();
                });
            }

            list.Add(row);
        }

        if (clans.Count == 0)
        {
            Label empty = new Label("Aucune");
            empty.AddToClassList("clan-empty");
            empty.AddToClassList("l2-color-3");
            list.Add(empty);
        }
    }
}
