class py_npc_strike_ca extends Emitter;

defaultproperties
{
     Begin Object Class=MeshEmitter Name=MeshEmitter14
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Skill_Power.skill_charge00'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.700000,Max=0.700000),Z=(Min=0.500000,Max=0.500000))
         Opacity=0.400000
         FadeOutStartTime=0.176000
         FadeOut=True
         FadeInEndTime=0.080000
         FadeIn=True
         MaxParticles=11
         ForcedMaxParticles=True
         RespawnDeadParticles=False
         Name="Wavebase"
         StartLocationOffset=(Z=2.000000)
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=0.100000,Max=0.200000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSpinRange=(X=(Min=0.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.700000)
         SizeScale(1)=(RelativeTime=1.000000,RelativeSize=2.500000)
         StartSizeRange=(X=(Min=0.143000,Max=0.143000),Y=(Min=0.143000,Max=0.143000),Z=(Min=0.048000,Max=0.048000))
         InitialParticlesPerSecond=8.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=0.400000,Max=0.400000)
     End Object
     Emitters(0)=MeshEmitter'MeshEmitter14'
     bNoDelete=False
     DrawScale=0.200000
     bDirectional=True
}
