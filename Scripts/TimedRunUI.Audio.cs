#nullable enable

using Godot;

public partial class TimedRunUI : Control
{
    private bool _audioInitLogged;

    private void EnsureAudio()
    {
        // Important: on ne charge plus de streams ni ne crée de players ici.
        // Tous les sons viennent des AudioStreamPlayer3D posés dans la scène (Main3D.tscn / Audio3D).
        var scene = GetTree().CurrentScene;
        if (scene == null)
        {
            // Défensif: dans certains ordres d'init, CurrentScene peut être null pendant _Ready.
            // On tente une résolution depuis la racine.
            scene = GetTree().Root.GetChildCount() > 0 ? GetTree().Root.GetChild(GetTree().Root.GetChildCount() - 1) : null;
            if (scene == null)
                return;
        }

        _audio3dRoot = scene.GetNodeOrNull<Node3D>("Audio3D");
        _sfxFlip = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxFlip");
        _sfxDraw = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxDraw");
        _sfxShuffle = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxShuffle");
        _sfxCorrect = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxCorrect");
        _sfxWrong = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxWrong");
        _ambience = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/Ambience");

        if (!IsInstanceValid(_sfxFlip) && !IsInstanceValid(_sfxDraw) && !IsInstanceValid(_ambience))
            GD.PushWarning("[MiniJeuCartesAWS] Audio3D introuvable (attendu: Audio3D/SfxFlip,SfxDraw,SfxShuffle,SfxCorrect,SfxWrong,Ambience)");
        else if (!_audioInitLogged)
        {
            _audioInitLogged = true;
            GD.Print($"[MiniJeuCartesAWS] Audio3D OK: flip={_sfxFlip?.Stream?.ResourcePath ?? "null"}, draw={_sfxDraw?.Stream?.ResourcePath ?? "null"}, shuffle={_sfxShuffle?.Stream?.ResourcePath ?? "null"}, correct={_sfxCorrect?.Stream?.ResourcePath ?? "null"}, wrong={_sfxWrong?.Stream?.ResourcePath ?? "null"}, amb={_ambience?.Stream?.ResourcePath ?? "null"}");
        }

        // Si le bus custom n'existe pas (layout non chargé), fallback sur Master.
        var hasSfxBus = AudioServer.GetBusIndex("SFX") >= 0;
        var hasAmbienceBus = AudioServer.GetBusIndex("Ambience") >= 0;
        if (!hasSfxBus)
        {
            if (IsInstanceValid(_sfxFlip)) _sfxFlip!.Bus = "Master";
            if (IsInstanceValid(_sfxDraw)) _sfxDraw!.Bus = "Master";
            if (IsInstanceValid(_sfxShuffle)) _sfxShuffle!.Bus = "Master";
            if (IsInstanceValid(_sfxCorrect)) _sfxCorrect!.Bus = "Master";
            if (IsInstanceValid(_sfxWrong)) _sfxWrong!.Bus = "Master";
        }
        if (!hasAmbienceBus)
        {
            if (IsInstanceValid(_ambience)) _ambience!.Bus = "Master";
        }

        if (!EnableAudio)
        {
            if (IsInstanceValid(_ambience))
                _ambience!.Stop();
            return;
        }

        // Volumes runtime (optionnel: la scène peut aussi les piloter)
        var sfxDb = Mathf.Clamp(SfxVolumeDb, -80f, 6f);
        var ambDb = Mathf.Clamp(AmbienceVolumeDb, -80f, 6f);
        if (IsInstanceValid(_sfxFlip)) _sfxFlip!.VolumeDb = sfxDb;
        if (IsInstanceValid(_sfxDraw)) _sfxDraw!.VolumeDb = sfxDb;
        if (IsInstanceValid(_sfxShuffle)) _sfxShuffle!.VolumeDb = sfxDb;
        if (IsInstanceValid(_sfxCorrect)) _sfxCorrect!.VolumeDb = sfxDb;
        if (IsInstanceValid(_sfxWrong)) _sfxWrong!.VolumeDb = sfxDb;
        if (IsInstanceValid(_ambience)) _ambience!.VolumeDb = ambDb;

        // Ambience
        if (!EnableAmbience)
        {
            if (IsInstanceValid(_ambience))
                _ambience!.Stop();
        }
        else
        {
            if (IsInstanceValid(_ambience) && _ambience!.Stream != null && !_ambience.Playing)
                _ambience.Play();
        }
    }

    private void PlaySfx(AudioStreamPlayer3D? player, float pitch = 1.0f)
    {
        if (!EnableAudio)
            return;

        if (!IsInstanceValid(player))
            return;
        if (player!.Stream == null)
            return;

        player.VolumeDb = SfxVolumeDb;
        player.PitchScale = pitch;

        // Re-trigger propre (si déjà en cours, on repart du début).
        if (player.Playing)
            player.Stop();
        player.Play();
    }
}
