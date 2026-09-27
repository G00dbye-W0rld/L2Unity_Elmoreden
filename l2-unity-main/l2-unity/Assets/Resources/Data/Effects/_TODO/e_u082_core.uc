class e_u082_core extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter13
         UseDirectionAs=PTDU_Normal
         ProjectionNormal=(X=1.000000)
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         MaxParticles=1
         RespawnDeadParticles=False
         Name="SpriteEmitter0"
         StartLocationOffset=(Z=-8.000000)
         StartSizeRange=(X=(Min=59.000000,Max=59.000000),Y=(Min=59.000000,Max=59.000000),Z=(Min=59.000000,Max=59.000000))
         UniformSize=True
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_AlphaBlend
         Texture=Texture'None'
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter13'
     Begin Object Class=MeshEmitter Name=MeshEmitter16
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.White.white_option00'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.600000
         FadeOutStartTime=1.600000
         FadeOut=True
         FadeInEndTime=0.560000
         FadeIn=True
         MaxParticles=1
         RespawnDeadParticles=False
         Name="Trail0"
         SpinParticles=True
         SpinCCWorCW=()
         StartSpinRange=(X=(Min=0.500000,Max=0.500000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSizeRange=(X=(Min=0.427000,Max=0.427000),Y=(Min=0.427000,Max=0.427000),Z=(Min=0.427000,Max=0.427000))
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=2.000000,Max=2.000000)
         InitialDelayRange=(Min=0.500000,Max=0.500000)
     End Object
     Emitters(1)=MeshEmitter'MeshEmitter16'
     Begin Object Class=MeshEmitter Name=MeshEmitter17
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.White.white_option00'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.600000
         FadeOutStartTime=1.280000
         FadeOut=True
         FadeInEndTime=0.512000
         FadeIn=True
         MaxParticles=1
         RespawnDeadParticles=False
         Name="Trail1"
         SpinParticles=True
         SpinCCWorCW=(Y=0.500000,Z=1.000000)
         StartSpinRange=(X=(Min=0.500000,Max=0.500000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.020000,Max=0.020000))
         StartSizeRange=(X=(Min=0.427000,Max=0.427000),Y=(Min=0.427000,Max=0.427000),Z=(Min=0.427000,Max=0.427000))
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=1.600000,Max=1.600000)
         InitialDelayRange=(Min=0.800000,Max=0.800000)
     End Object
     Emitters(2)=MeshEmitter'MeshEmitter17'
     Begin Object Class=MeshEmitter Name=MeshEmitter18
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.White.white_option01'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         Opacity=0.200000
         FadeOutStartTime=1.200000
         FadeOut=True
         FadeInEndTime=0.495000
         FadeIn=True
         RespawnDeadParticles=False
         Name="Ring"
         StartLocationRange=(X=(Min=-2.000000,Max=2.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         SpinParticles=True
         SpinCCWorCW=(X=0.500000,Y=0.500000)
         SpinsPerSecondRange=(X=(Min=0.000000,Max=0.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=1.000000,Max=1.000000))
         StartSpinRange=(X=(Min=0.000000,Max=0.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=1.000000))
         StartSizeRange=(X=(Min=0.422000,Max=0.443000),Y=(Min=0.422000,Max=0.443000),Z=(Min=0.422000,Max=0.443000))
         UniformSize=True
         InitialParticlesPerSecond=20.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=1.500000,Max=1.500000)
         InitialDelayRange=(Min=0.500000,Max=0.500000)
     End Object
     Emitters(3)=MeshEmitter'MeshEmitter18'
     Begin Object Class=BeamEmitter Name=BeamEmitter2
         BeamDistanceRange=(Min=50.000000,Max=50.000000)
         BeamEndPoints(0)=(ActorTag=None,offset=(X=(Min=0.000000,Max=0.000000),Y=(Min=-15.000000,Max=15.000000),Z=(Min=40.000000,Max=40.000000)),Weight=0.250000)
         BeamEndPoints(1)=(ActorTag=None,offset=(X=(Min=0.000000,Max=0.000000),Y=(Min=-15.000000,Max=15.000000),Z=(Min=-40.000000,Max=-40.000000)),Weight=0.250000)
         BeamEndPoints(2)=(ActorTag=None,offset=(X=(Min=0.000000,Max=0.000000),Y=(Min=40.000000,Max=40.000000),Z=(Min=-15.000000,Max=15.000000)),Weight=0.250000)
         BeamEndPoints(3)=(ActorTag=None,offset=(X=(Min=0.000000,Max=0.000000),Y=(Min=-40.000000,Max=-40.000000),Z=(Min=-15.000000,Max=15.000000)),Weight=0.250000)
         DetermineEndPointBy=2
         HighFrequencyNoiseRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=-1.000000,Max=1.000000),Z=(Min=-1.000000,Max=1.000000))
         NoiseDeterminesEndPoint=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         FadeOutStartTime=0.800000
         FadeOut=True
         CoordinateSystem=PTCS_Independent
         MaxParticles=8
         RespawnDeadParticles=False
         Disabled=True
         Name="BeamEmitter0"
         StartSizeRange=(X=(Min=8.000000,Max=8.000000),Y=(Min=8.000000,Max=8.000000),Z=(Min=8.000000,Max=8.000000))
         InitialParticlesPerSecond=8.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Spark.fx_m_t0042'
         LifetimeRange=(Min=0.300000,Max=0.300000)
         InitialDelayRange=(Min=1.000000,Max=1.000000)
     End Object
     Emitters(4)=BeamEmitter'BeamEmitter2'
     bNoDelete=False
     bDirectional=True
}
