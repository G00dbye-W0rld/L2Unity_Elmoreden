class s_u023_lightening_ca extends Emitter;

defaultproperties
{
     Begin Object Class=MeshEmitter Name=MeshEmitter13
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Monster.wispray01'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.200000
         FadeOut=True
         MaxParticles=4
         RespawnDeadParticles=False
         Name="shine mesh"
         StartLocationShape=PTLS_Polar
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=0.300000,Max=0.300000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.900000,RelativeSize=1.000000)
         SizeScale(1)=(RelativeTime=1.000000,RelativeSize=0.000000)
         StartSizeRange=(X=(Min=0.340000,Max=0.340000),Y=(Min=0.340000,Max=0.340000),Z=(Min=0.340000,Max=0.340000))
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_Brighten
         LifetimeRange=(Min=3.000000,Max=3.000000)
     End Object
     Emitters(0)=MeshEmitter'MeshEmitter13'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter5
         UseDirectionAs=PTDU_Up
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=0,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScaleRepeats=3.000000
         FadeOutStartTime=0.704000
         FadeOut=True
         FadeInEndTime=0.072000
         FadeIn=True
         MaxParticles=40
         WeatherSoundCheck=True
         RespawnDeadParticles=False
         Name="Particles"
         StartLocationShape=PTLS_Sphere
         SphereRadiusRange=(Min=30.000000,Max=35.000000)
         StartLocationPolarRange=(X=(Min=90.000000,Max=90.000000),Y=(Min=0.000000,Max=360.000000),Z=(Min=30.000000,Max=30.000000))
         StartSpinRange=(X=(Min=0.000000,Max=0.500000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UniformSize=True
         StartSizeRange=(X=(Min=2.000000,Max=2.000000),Y=(Min=1.000000,Max=1.000000),Z=(Min=1.000000,Max=1.000000))
         InitialParticlesPerSecond=20.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_Brighten
         Texture=Texture'LineageEffectsTextures.Particles3.fx_m_t6012'
         LifetimeRange=(Min=0.800000,Max=0.800000)
         StartVelocityRange=(X=(Min=50.000000,Max=50.000000),Y=(Min=50.000000,Max=50.000000),Z=(Min=50.000000,Max=50.000000))
         VelocityLossRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=1.000000,Max=1.000000),Z=(Min=1.000000,Max=1.000000))
         GetVelocityDirectionFrom=PTVD_StartPositionAndOwner
     End Object
     Emitters(1)=SpriteEmitter'SpriteEmitter5'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter1
         UseColorScale=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=47,G=47,R=47,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScaleRepeats=20.000000
         Opacity=0.400000
         FadeOutStartTime=2.400000
         FadeOut=True
         MaxParticles=5
         RespawnDeadParticles=False
         Name="smoke"
         UniformSize=True
         StartSizeRange=(X=(Min=21.000000,Max=21.000000),Y=(Min=21.000000,Max=21.000000),Z=(Min=21.000000,Max=21.000000))
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles3.fx_m_t6006'
         TextureUSubdivisions=2
         TextureVSubdivisions=2
         BlendBetweenSubdivisions=True
         UseRandomSubdivision=True
         SubdivisionEnd=3
         LifetimeRange=(Min=1.500000,Max=1.500000)
     End Object
     Emitters(2)=SpriteEmitter'SpriteEmitter1'
     Begin Object Class=MeshEmitter Name=MeshEmitter0
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Skill_Power.skill_charge01'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=0.600000,Max=0.600000),Y=(Min=0.600000,Max=0.600000),Z=(Min=1.000000,Max=1.000000))
         Opacity=0.800000
         FadeOutStartTime=1.060000
         FadeOut=True
         FadeInEndTime=0.140000
         FadeIn=True
         MaxParticles=8
         RespawnDeadParticles=False
         Name="elec mesh"
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=0.500000,Max=1.000000),Y=(Min=0.500000,Max=1.000000),Z=(Min=0.500000,Max=1.000000))
         StartSpinRange=(X=(Min=0.000000,Max=1.000000),Y=(Min=0.000000,Max=1.000000),Z=(Min=0.000000,Max=1.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.900000,RelativeSize=1.000000)
         SizeScale(1)=(RelativeTime=1.000000,RelativeSize=0.000000)
         StartSizeRange=(X=(Min=0.652000,Max=0.652000),Y=(Min=0.652000,Max=0.652000),Z=(Min=0.652000,Max=0.652000))
         InitialParticlesPerSecond=5.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=1.000000,Max=2.000000)
     End Object
     Emitters(3)=MeshEmitter'MeshEmitter0'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter7
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.400000
         FadeOutStartTime=2.460000
         FadeOut=True
         MaxParticles=2
         RespawnDeadParticles=False
         Name="glow"
         SpinParticles=True
         SpinCCWorCW=(X=1.000000,Y=0.500000,Z=0.500000)
         SpinsPerSecondRange=(X=(Min=0.200000,Max=0.200000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         UniformSize=True
         SizeScale(0)=(RelativeTime=1.000000,RelativeSize=0.800000)
         StartSizeRange=(X=(Min=25.000000,Max=25.000000),Y=(Min=25.000000,Max=25.000000),Z=(Min=25.000000,Max=25.000000))
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles3.fx_m_t6014'
         LifetimeRange=(Min=3.000000,Max=3.000000)
     End Object
     Emitters(4)=SpriteEmitter'SpriteEmitter7'
     bLightChanged=True
     bNoDelete=False
     bSunAffect=True
     DrawScale=0.200000
}
