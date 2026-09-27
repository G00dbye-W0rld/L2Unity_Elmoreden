class e_u300_heart_fl extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter9
         Acceleration=(X=-42.783001)
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=0.228571,Color=(B=0,G=0,R=0,A=255))
         ColorScale(2)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.800000
         FadeOutStartTime=0.320000
         FadeOut=True
         CoordinateSystem=PTCS_Spray
         MaxParticles=53
         Name="heart particle"
         StartLocationRange=(X=(Min=-1.750000,Max=1.750000),Y=(Min=-1.750000,Max=1.750000),Z=(Min=-1.750000,Max=1.750000))
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=0.000000,Max=3.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSpinRange=(X=(Min=0.500000,Max=0.500000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.800000)
         SizeScale(1)=(RelativeTime=1.000000,RelativeSize=0.600000)
         SizeScaleRepeats=10.000000
         StartSizeRange=(X=(Min=0.700000,Max=1.050000),Y=(Min=0.700000,Max=1.050000),Z=(Min=0.700000,Max=1.050000))
         UniformSize=True
         InitialParticlesPerSecond=10.000000
         DrawStyle=PTDS_Brighten
         Texture=Texture'LineageEffectsTextures.Particles3.fx_m_t6002'
         TextureUSubdivisions=2
         TextureVSubdivisions=2
         SubdivisionEnd=1
         LifetimeRange=(Min=1.250000,Max=1.250000)
         StartVelocityRange=(X=(Min=-1.750000,Max=-1.750000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter9'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter4
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         MaxParticles=1
         Name="heart"
         SpinParticles=True
         StartSpinRange=(X=(Min=0.500000,Max=0.500000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.900000)
         SizeScale(1)=(RelativeTime=0.500000,RelativeSize=1.200000)
         SizeScale(2)=(RelativeTime=1.000000,RelativeSize=0.900000)
         StartSizeRange=(X=(Min=2.772000,Max=2.772000),Y=(Min=2.772000,Max=2.772000),Z=(Min=2.772000,Max=2.772000))
         UniformSize=True
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_Brighten
         Texture=Texture'LineageEffectsTextures.Particles3.fx_m_t6002'
         TextureUSubdivisions=2
         TextureVSubdivisions=2
         SubdivisionEnd=1
         LifetimeRange=(Min=0.510000,Max=0.510000)
     End Object
     Emitters(1)=SpriteEmitter'SpriteEmitter4'
     Physics=10
     bUseDynamicLights=False
     bLightChanged=True
     bTrailerSameRotation=True
     bTrailerPrePivot=True
     bTrailerNoOwnerDestroy=True
     bAcceptsProjectors=False
     bSunAffect=True
     DrawScale=0.100000
}
