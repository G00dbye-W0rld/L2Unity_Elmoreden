using System.Collections.Generic;

// Ce que raconte chaque emote dans le canal Role Play. Les numeros sont ceux de
// ActionName, que le serveur rediffuse tels quels.
public static class SocialActionText
{
    private static readonly Dictionary<int, string> _texts = new Dictionary<int, string>
    {
        { 2, "salue de la main" },
        { 3, "lève les bras en signe de victoire" },
        { 4, "lance la charge" },
        { 5, "fait non de la tête" },
        { 6, "acquiesce" },
        { 7, "s'incline avec respect" },
        { 8, "hausse les épaules, l'air perdu" },
        { 9, "attend, visiblement impatient" },
        { 10, "éclate de rire" },
        { 11, "applaudit" },
        { 12, "se met à danser" },
        { 13, "baisse la tête, accablé" },
        { 14, "prend une pose charmeuse" },
        { 15, "rougit et se fait tout petit" },
        { 16, "échange une révérence" },
        { 17, "tape dans la main" },
        { 18, "danse à deux" },
        { 19, "met un genou à terre et fait sa demande" },
        { 20, "toise son adversaire d'un air provocateur" },
        { 21, "se met en valeur, très content de soi" },
    };

    // La voix que le client associe a chaque emote, dans son paquet ChrSound.
    private static readonly Dictionary<int, EntitySoundEvent> _voices = new Dictionary<int, EntitySoundEvent>
    {
        { 2, EntitySoundEvent.Greeting },
        { 3, EntitySoundEvent.Victory },
        { 4, EntitySoundEvent.Followme },
        { 5, EntitySoundEvent.Angry },
        { 6, EntitySoundEvent.Agree },
        { 7, EntitySoundEvent.Salute },
        { 8, EntitySoundEvent.Wonder },
        { 9, EntitySoundEvent.Tired },
        { 10, EntitySoundEvent.Laugh },
        { 11, EntitySoundEvent.Cheer },
        { 12, EntitySoundEvent.Dance },
        { 13, EntitySoundEvent.Cry },
        { 14, EntitySoundEvent.Humming },
        { 15, EntitySoundEvent.Scare },
        // Les emotes a deux reprennent la voix de leur equivalent solo.
        { 16, EntitySoundEvent.Salute },
        { 17, EntitySoundEvent.Cheer },
        { 18, EntitySoundEvent.Dance },
        { 19, EntitySoundEvent.Admiration },
        { 20, EntitySoundEvent.Tease_1 },
        { 21, EntitySoundEvent.Warmup },
    };

    public static string Get(int action)
    {
        return _texts.TryGetValue(action, out string text) ? text : null;
    }

    public static bool TryGetVoice(int action, out EntitySoundEvent voice)
    {
        return _voices.TryGetValue(action, out voice);
    }
}
