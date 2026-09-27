class at_shield_stun_cs extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter4
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=0.660000,Max=0.660000),Y=(Min=1.000000,Max=1.000000),Z=(Min=1.000000,Max=1.000000))
         MaxParticles=1
         RespawnDeadParticles=False
         Name="blackback"
         StartLocationRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         SpinParticles=True
         SpinsPerSecondRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         StartSpinRange=(X=(Min=-1.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=1.000000)
         SizeScale(1)=(RelativeTime=0.600000,RelativeSize=0.000000)
         SizeScale(2)=(RelativeTime=1.000000,RelativeSize=0.000000)
         StartSizeRange=(X=(Min=39.000000,Max=39.000000),Y=(Min=39.000000,Max=39.000000),Z=(Min=39.000000,Max=39.000000))
         UniformSize=True
         InitialParticlesPerSecond=70.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_Darken
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0105'
         LifetimeRange=(Min=0.300000,Max=0.300000)
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter4'
     bNoDelete=False
     DrawScale=0.200000
     bDirectional=True
}
