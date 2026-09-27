class el_aqua_swirl_pt extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter11
         Acceleration=(Z=-120.000000)
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         FadeOutStartTime=0.255000
         FadeOut=True
         FadeInEndTime=0.105000
         FadeIn=True
         RespawnDeadParticles=False
         Name="Shoot"
         StartLocationOffset=(X=5.000000)
         StartLocationShape=PTLS_Polar
         StartLocationPolarRange=(X=(Min=90.000000,Max=90.000000),Y=(Min=0.000000,Max=360.000000),Z=(Min=10.000000,Max=10.000000))
         SpinParticles=True
         StartSpinRange=(X=(Min=-0.100000,Max=0.100000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=1.000000,RelativeSize=1.700000)
         StartSizeRange=(X=(Min=3.000000,Max=5.000000),Y=(Min=3.000000,Max=5.000000),Z=(Min=3.000000,Max=5.000000))
         UniformSize=True
         InitialParticlesPerSecond=10000.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_AlphaBlend
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0099'
         TextureUSubdivisions=2
         TextureVSubdivisions=2
         SubdivisionEnd=3
         UseRandomSubdivision=True
         LifetimeRange=(Min=0.500000,Max=1.500000)
         StartVelocityRange=(X=(Min=-30.000000,Max=-30.000000),Y=(Min=20.000000,Max=20.000000),Z=(Min=20.000000,Max=20.000000))
         VelocityLossRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=1.000000,Max=1.000000),Z=(Min=1.000000,Max=1.000000))
         GetVelocityDirectionFrom=PTVD_OwnerAndStartPosition
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter11'
     bNoDelete=False
     DrawScale=0.100000
     bDirectional=True
}
