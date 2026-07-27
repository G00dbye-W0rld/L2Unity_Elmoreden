public class ChatMessage
{
    protected string _user;
    protected string _message;

    // Instancie, pas static : un champ static "Type" redeclare (masque, pas
    // surcharge - les champs C# n'ont pas de polymorphisme) dans chaque
    // sous-classe faisait que MessageType renvoyait TOUJOURS ALL, la valeur
    // du champ de CETTE classe de base, quelle que soit la sous-classe reelle
    // instanciee - bug trouve en ajoutant l'onglet de chat "Groupe" (filtre
    // sur PARTY, ne recevait jamais rien a cause de ça).
    public L2MessageType MessageType { get; }

    public ChatMessage(string user, string message) : this(user, message, L2MessageType.ALL)
    {
    }

    protected ChatMessage(string user, string message, L2MessageType type)
    {
        _user = user;
        _message = message;
        MessageType = type;
    }

    public override string ToString()
    {
        return "<color=#B09B79>" + _user + ": " + _message + "</color>";
    }
}
