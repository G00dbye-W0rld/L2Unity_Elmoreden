class el_wind_strike_pr extends Emitter;

defaultproperties
{
     Begin Object Class=MeshEmitter Name=MeshEmitter8
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Wind.windblowin00'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=0.900000,Max=0.900000),Y=(Min=0.950000,Max=0.950000),Z=(Min=1.000000,Max=1.000000))
         FadeOutFactor=(W=1.000000,X=0.300000,Y=0.300000,Z=0.300000)
         FadeOutStartTime=0.200000
         FadeOut=True
         FadeInFactor=(W=1.000000,X=0.300000,Y=0.300000,Z=0.300000)
         FadeInEndTime=0.100000
         FadeIn=True
         MaxParticles=100
         ResetAfterChange=True
         ForcedMaxParticles=True
         RespawnDeadParticles=False
         Name="Wind"
         StartLocationRange=(X=(Min=0.000000,Max=0.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=-3.000000,Max=3.000000))
         SpinParticles=True
         SpinCCWorCW=(Y=0.500000,Z=0.500000)
         SpinsPerSecondRange=(X=(Min=1.800000,Max=1.800000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSpinRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.600000)
         SizeScale(1)=(RelativeTime=0.200000,RelativeSize=0.500000)
         SizeScale(2)=(RelativeTime=1.000000,RelativeSize=0.100000)
         StartSizeRange=(X=(Min=0.200000,Max=0.200000),Y=(Min=0.200000,Max=0.200000),Z=(Min=0.100000,Max=0.200000))
         InitialParticlesPerSecond=20.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_AlphaBlend
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0000'
         LifetimeRange=(Min=0.500000,Max=0.500000)
     End Object
     Emitters(0)=MeshEmitter'MeshEmitter8'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter10
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=0.700000,Max=0.700000),Y=(Min=0.700000,Max=0.700000),Z=(Min=0.700000,Max=0.700000))
         FadeOut=True
         MaxParticles=200
         ForcedMaxParticles=True
         RespawnDeadParticles=False
         Name="Core"
         SpinParticles=True
         StartSpinRange=(X=(Min=0.000000,Max=360.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSizeRange=(X=(Min=7.000000,Max=7.000000),Y=(Min=100.000000,Max=100.000000),Z=(Min=100.000000,Max=100.000000))
         UniformSize=True
         InitialParticlesPerSecond=40.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0000'
         TextureUSubdivisions=4
         TextureVSubdivisions=4
         SubdivisionStart=7
         SubdivisionEnd=8
         UseRandomSubdivision=True
         LifetimeRange=(Min=0.100000,Max=0.100000)
     End Object
     Emitters(1)=SpriteEmitter'SpriteEmitter10'
     bNoDelete=False
     Rotation=(Pitch=0,Yaw=32892,Roll=0)
     DrawScale=0.100000
}
