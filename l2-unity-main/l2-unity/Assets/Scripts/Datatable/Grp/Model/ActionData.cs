using System.Collections;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Xml.Linq;
using UnityEngine;

public class ActionData
{
    [SerializeField] private int _id;
    [SerializeField] private string _name;
    [SerializeField] private string _description;
    [SerializeField] private string _icon;

    [SerializeField] private int _type;
    [SerializeField] private int _category;
    [SerializeField] private string _command;

    public int Id { get => _id; set => _id = value; }
    public string Name { get => _name; set => _name = value; }
    public string Descripion { get => _description; set => _description = value; }
    public string Icon { get => _icon; set => _icon = value; }

    // Emotes : categorie 4, et "type" porte l'identifiant social attendu par le serveur.
    public int Type { get => _type; set => _type = value; }
    public int Category { get => _category; set => _category = value; }

    // La commande de chat, sans la barre : "socialbow", "sitstand"...
    public string Command { get => _command; set => _command = value; }

    public bool IsSocial { get { return _category == 4 && _type >= 2 && _type <= 21; } }

    // Les emotes a deux (saluts croises, tape dans la main, danse) demandent une cible
    // et son accord : le client passe par un autre paquet.
    public bool IsCouple { get { return _type >= 16 && _type <= 18; } }
}
