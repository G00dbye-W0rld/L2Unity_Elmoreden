class mo_majesty_ta extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter8
         UseColorScale=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=0.539286,Color=(B=156,G=129,R=137,A=255))
         ColorScale(2)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScaleRepeats=13.000000
         Opacity=0.600000
         FadeOutStartTime=0.080000
         FadeOut=True
         MaxParticles=2
         RespawnDeadParticles=False
         Name="Fireon"
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=0.000000,Max=0.010000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.200000)
         SizeScale(1)=(RelativeTime=0.060000,RelativeSize=1.500000)
         SizeScale(2)=(RelativeTime=0.110000,RelativeSize=2.500000)
         SizeScale(3)=(RelativeTime=0.220000,RelativeSize=2.550000)
         SizeScale(4)=(RelativeTime=1.000000,RelativeSize=2.500000)
         StartSizeRange=(X=(Min=14.000000,Max=14.000000),Y=(Min=14.000000,Max=14.000000),Z=(Min=14.000000,Max=14.000000))
         UniformSize=True
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles2.fx_m_t1017'
         TextureUSubdivisions=2
         TextureVSubdivisions=2
         BlendBetweenSubdivisions=True
         SubdivisionEnd=3
         LifetimeRange=(Min=0.800000,Max=0.800000)
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter8'
     Begin Object Class=MeshEmitter Name=MeshEmitter8
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Support.supportenchant01'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=50,G=1,R=54,A=255))
         ColorMultiplierRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.400000,Max=0.400000),Z=(Min=0.100000,Max=0.100000))
         FadeOutStartTime=0.072000
         FadeOut=True
         MaxParticles=3
         RespawnDeadParticles=False
         Name="Lightbeam"
         SpinParticles=True
         StartSpinRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.200000)
         SizeScale(1)=(RelativeTime=0.050000,RelativeSize=1.000000)
         SizeScale(2)=(RelativeTime=0.170000,RelativeSize=2.000000)
         SizeScale(3)=(RelativeTime=1.000000,RelativeSize=2.200000)
         StartSizeRange=(X=(Min=0.280000,Max=0.280000),Y=(Min=0.280000,Max=0.280000),Z=(Min=0.280000,Max=0.280000))
         InitialParticlesPerSecond=10000.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=0.400000,Max=0.400000)
     End Object
     Emitters(1)=MeshEmitter'MeshEmitter8'
     bNoDelete=False
     DrawScale=0.200000
     bDirectional=True
}
