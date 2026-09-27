class el_eruption_cs extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter4
         UseDirectionAs=PTDU_Normal
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=1.000000,Max=1.000000),Y=(Min=0.422000,Max=0.422000),Z=(Min=0.422000,Max=0.422000))
         Opacity=0.400000
         FadeOutStartTime=0.024000
         FadeOut=True
         MaxParticles=3
         RespawnDeadParticles=False
         Name="Shot"
         StartLocationOffset=(Z=4.000000)
         SpinParticles=True
         StartSpinRange=(X=(Min=0.000000,Max=1.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseSizeScale=True
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.000000,RelativeSize=1.000000)
         SizeScale(1)=(RelativeTime=0.100000,RelativeSize=1.400000)
         SizeScale(2)=(RelativeTime=0.300000,RelativeSize=1.600000)
         SizeScale(3)=(RelativeTime=1.000000,RelativeSize=1.800000)
         StartSizeRange=(X=(Min=23.000000,Max=23.000000),Y=(Min=23.000000,Max=23.000000),Z=(Min=23.000000,Max=23.000000))
         UniformSize=True
         InitialParticlesPerSecond=10.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0138'
         LifetimeRange=(Min=0.400000,Max=0.400000)
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter4'
     bNoDelete=False
     DrawScale=0.100000
     bDirectional=True
}
