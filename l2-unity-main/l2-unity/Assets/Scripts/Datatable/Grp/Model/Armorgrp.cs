using UnityEngine;

[System.Serializable]
public class Armorgrp : Abstractgrp {
   
    [SerializeField] private int _mpBonus;
    [SerializeField] private ItemSlot _bodypart;
    [SerializeField] private string[] _model;
    [SerializeField] private string[] _texture;
    [SerializeField] private string[] _lowerModel;
    [SerializeField] private string[] _lowerTexture;

    public int MpBonus { get { return _mpBonus; } set { _mpBonus = value; } }
    public ItemSlot BodyPart { get { return _bodypart; } set { _bodypart = value; } }
    public string[] Model { get { return _model; } set { _model = value; } }
    public string[] Texture { get { return _texture; } set { _texture = value; } }
    // Armure complete : piece du bas, portee en plus de Model (haut du corps).
    public string[] LowerModel { get { return _lowerModel; } set { _lowerModel = value; } }
    public string[] LowerTexture { get { return _lowerTexture; } set { _lowerTexture = value; } }
}
