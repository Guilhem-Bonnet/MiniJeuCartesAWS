#nullable enable

using Godot;

public partial class TimedRunUI : Control
{
    private void EnsureAudio()
    {
        // Important: on ne charge plus de streams ni ne crée de players ici.
        // Tous les sons viennent des AudioStreamPlayer3D posés dans la scène (Main3D.tscn / Audio3D).
        var scene = GetTree().CurrentScene;
        if (scene == null)
            return;

        _audio3dRoot = scene.GetNodeOrNull<Node3D>("Audio3D");
        _sfxFlip = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxFlip");
        _sfxDraw = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxDraw");
        _sfxShuffle = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxShuffle");
        _sfxCorrect = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxCorrect");
        _sfxWrong = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/SfxWrong");
        _ambience = scene.GetNodeOrNull<AudioStreamPlayer3D>("Audio3D/Ambience");

        if (!EnableAudio)
        {
            if (IsInstanceValid(_ambience))
                _ambience!.Stop();
            return;
        }

        // Volumes runtime (optionnel: la scène peut aussi les piloter)
        if (IsInstanceValid(_sfxFlip)) _sfxFlip!.VolumeDb = SfxVolumeDb;
        if (IsInstanceValid(_sfxDraw)) _sfxDraw!.VolumeDb = SfxVolumeDb;
        if (IsInstanceValid(_sfxShuffle)) _sfxShuffle!.VolumeDb = SfxVolumeDb;
        if (IsInstanceValid(_sfxCorrect)) _sfxCorrect!.VolumeDb = SfxVolumeDb;
        if (IsInstanceValid(_sfxWrong)) _sfxWrong!.VolumeDb = SfxVolumeDb;
        if (IsInstanceValid(_ambience)) _ambience!.VolumeDb = AmbienceVolumeDb;

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
