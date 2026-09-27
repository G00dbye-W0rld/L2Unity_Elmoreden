class mo_battle_roar_ta extends Emitter;

defaultproperties
{
     Begin Object Class=MeshEmitter Name=MeshEmitter3
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Impact.shockwave00'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=0,G=0,R=0,A=255))
         ColorMultiplierRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.790000,Max=0.790000),Z=(Min=0.440000,Max=0.440000))
         FadeOutStartTime=0.015000
         FadeOut=True
         MaxParticles=1
         RespawnDeadParticles=False
         Name="1"
         StartLocationOffset=(Z=-32.737999)
         SpinParticles=True
         StartSpinRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=0.250000,Max=0.250000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=0.200000)
         SizeScale(1)=(RelativeTime=0.110000,RelativeSize=1.000000)
         SizeScale(2)=(RelativeTime=0.400000,RelativeSize=2.000000)
         SizeScale(3)=(RelativeTime=1.000000,RelativeSize=2.420000)
         StartSizeRange=(X=(Min=0.721000,Max=0.721000),Y=(Min=0.182000,Max=0.182000),Z=(Min=0.182000,Max=0.182000))
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         LifetimeRange=(Min=0.300000,Max=0.300000)
     End Object
     Emitters(0)=MeshEmitter'MeshEmitter3'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter3
         Acceleration=(Z=10.000000)
         UseColorScale=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=22,G=131,R=220,A=255))
         ColorScale(1)=(RelativeTime=0.821429,Color=(B=67,G=14,R=54,A=255))
         ColorScale(2)=(RelativeTime=1.000000,Color=(B=169,G=64,R=37,A=255))
         ColorMultiplierRange=(X=(Min=0.562000,Max=0.562000),Y=(Min=0.562000,Max=0.562000),Z=(Min=1.000000,Max=1.000000))
         Opacity=0.400000
         FadeOutStartTime=0.050000
         FadeOut=True
         RespawnDeadParticles=False
         Name="3"
         StartLocationOffset=(Z=10.000000)
         StartLocationRange=(X=(Min=-10.000000,Max=10.000000),Y=(Min=-10.000000,Max=10.000000),Z=(Min=-10.000000,Max=10.000000))
         StartLocationShape=PTLS_Sphere
         SphereRadiusRange=(Min=7.000000,Max=7.000000)
         StartSizeRange=(X=(Min=24.856001,Max=46.332001),Y=(Min=24.856001,Max=46.332001),Z=(Min=24.856001,Max=46.332001))
         UniformSize=True
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles2.fx_m_t1002'
         TextureUSubdivisions=2
         TextureVSubdivisions=4
         BlendBetweenSubdivisions=True
         SubdivisionEnd=3
         LifetimeRange=(Min=1.000000,Max=1.000000)
         StartVelocityRange=(X=(Min=200.000000,Max=200.000000),Y=(Min=200.000000,Max=200.000000),Z=(Min=100.000000,Max=100.000000))
         VelocityLossRange=(X=(Min=5.000000,Max=5.000000),Y=(Min=5.000000,Max=5.000000),Z=(Min=5.000000,Max=5.000000))
         GetVelocityDirectionFrom=PTVD_OwnerAndStartPosition
     End Object
     Emitters(1)=SpriteEmitter'SpriteEmitter3'
     bNoDelete=False
     DrawScale=0.200000
     bDirectional=True
}
