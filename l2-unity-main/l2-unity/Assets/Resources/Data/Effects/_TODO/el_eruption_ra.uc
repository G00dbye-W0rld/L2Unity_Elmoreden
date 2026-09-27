class el_eruption_ra extends Emitter;

defaultproperties
{
     Begin Object Class=MeshEmitter Name=MeshEmitter6
         StaticMesh=StaticMesh'LineageEffectsStaticmeshes.Fire.firebomb00'
         UseMeshBlendMode=False
         RenderTwoSided=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=208,G=176,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=152,G=67,R=135,A=255))
         ColorMultiplierRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.750000,Max=0.750000),Z=(Min=0.750000,Max=0.750000))
         Opacity=0.600000
         FadeOutStartTime=0.066000
         FadeOut=True
         FadeInEndTime=0.024000
         FadeIn=True
         MaxParticles=3
         RespawnDeadParticles=False
         Name="FlameMesh"
         StartLocationRange=(X=(Min=0.000000,Max=0.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=-5.000000,Max=5.000000))
         SphereRadiusRange=(Min=3.000000,Max=3.000000)
         SpinParticles=True
         SpinCCWorCW=(X=1.000000,Y=0.500000,Z=0.500000)
         SpinsPerSecondRange=(X=(Min=0.000000,Max=0.100000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSpinRange=(X=(Min=0.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.100000,RelativeSize=1.400000)
         SizeScale(1)=(RelativeTime=0.220000,RelativeSize=1.700000)
         SizeScale(2)=(RelativeTime=1.000000,RelativeSize=2.000000)
         StartSizeRange=(X=(Min=0.450000,Max=0.500000),Y=(Min=0.450000,Max=0.500000),Z=(Min=0.450000,Max=0.500000))
         UniformSize=True
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_Brighten
         LifetimeRange=(Min=0.600000,Max=0.600000)
     End Object
     Emitters(0)=MeshEmitter'MeshEmitter6'
     bNoDelete=False
     DrawScale=0.500000
     bDirectional=True
}
