class el_prominence_ca extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter12
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.200000
         FadeOutStartTime=0.800000
         FadeOut=True
         FadeInEndTime=0.800000
         FadeIn=True
         MaxParticles=5
         ForcedLifeTime=True
         ForcedFade=True
         RespawnDeadParticles=False
         Name="Center"
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=0.100000,Max=0.125000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSpinRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=1.000000)
         SizeScale(1)=(RelativeTime=1.000000,RelativeSize=2.000000)
         StartSizeRange=(X=(Min=10.000000,Max=10.000000),Y=(Min=10.000000,Max=10.000000),Z=(Min=10.000000,Max=10.000000))
         UniformSize=True
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0105'
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter12'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter20
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.050000
         FadeOutStartTime=0.060000
         FadeOut=True
         FadeInEndTime=0.033000
         FadeIn=True
         MaxParticles=40
         ForcedMaxParticles=True
         RespawnDeadParticles=False
         Name="Rainbow"
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.200000)
         SizeScale(1)=(RelativeTime=0.800000,RelativeSize=1.000000)
         SizeScale(2)=(RelativeTime=1.000000,RelativeSize=1.200000)
         StartSizeRange=(X=(Min=30.000000,Max=30.000000),Y=(Min=30.000000,Max=30.000000),Z=(Min=30.000000,Max=30.000000))
         UniformSize=True
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0056'
         LifetimeRange=(Min=0.300000,Max=0.300000)
     End Object
     Emitters(1)=SpriteEmitter'SpriteEmitter20'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter21
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         FadeOutStartTime=0.800000
         FadeOut=True
         FadeInEndTime=0.800000
         FadeIn=True
         ForcedLifeTime=True
         ForcedFade=True
         RespawnDeadParticles=False
         Name="Dust"
         SpinParticles=True
         StartSpinRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSizeRange=(X=(Min=6.000000,Max=10.000000),Y=(Min=6.000000,Max=10.000000),Z=(Min=6.000000,Max=10.000000))
         UniformSize=True
         InitialParticlesPerSecond=20.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures2.balakas.fx_m_t1037'
         TextureUSubdivisions=2
         TextureVSubdivisions=2
         SubdivisionEnd=3
         UseRandomSubdivision=True
         StartVelocityRange=(X=(Min=-5.000000,Max=5.000000),Y=(Min=-5.000000,Max=5.000000),Z=(Min=-5.000000,Max=5.000000))
         VelocityLossRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=1.000000,Max=1.000000),Z=(Min=1.000000,Max=1.000000))
     End Object
     Emitters(2)=SpriteEmitter'SpriteEmitter21'
     bLightChanged=True
     bNoDelete=False
     bSunAffect=True
     bDirectional=True
}
