class e_u034_b extends Emitter;

defaultproperties
{
     Begin Object Class=SpriteEmitter Name=SpriteEmitter8
         ProjectionNormal=(X=1.000000)
         UseColorScale=True
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=0.514286,Color=(B=128,G=128,R=128,A=255))
         ColorScale(2)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScaleRepeats=20.000000
         MaxParticles=20
         RespawnDeadParticles=False
         Name="Circle"
         StartLocationRange=(X=(Min=-38.000000,Max=0.000000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
         UseRegularSizeScale=False
         SizeScale(0)=(RelativeTime=0.600000,RelativeSize=1.000000)
         SizeScale(1)=(RelativeTime=1.000000,RelativeSize=0.500000)
         StartSizeRange=(X=(Min=6.000000,Max=6.000000),Y=(Min=5.000000,Max=5.000000),Z=(Min=5.000000,Max=5.000000))
         UniformSize=True
         InitialParticlesPerSecond=10000.000000
         AutomaticInitialSpawning=False
         DrawStyle=PTDS_Brighten
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0000'
         TextureUSubdivisions=8
         TextureVSubdivisions=8
         SubdivisionStart=11
         SubdivisionEnd=12
         UseRandomSubdivision=True
         LifetimeRange=(Min=2.000000,Max=2.000000)
     End Object
     Emitters(0)=SpriteEmitter'SpriteEmitter8'
     Begin Object Class=SpriteEmitter Name=SpriteEmitter9
         UseDirectionAs=PTDU_Up
         ColorScale(0)=(RelativeTime=0.000000,Color=(B=255,G=255,R=255,A=255))
         ColorScale(1)=(RelativeTime=1.000000,Color=(B=255,G=255,R=255,A=255))
         ColorMultiplierRange=(X=(Min=0.300000,Max=0.300000),Y=(Min=0.600000,Max=0.600000),Z=(Min=1.000000,Max=1.000000))
         MaxParticles=1
         RespawnDeadParticles=False
         Name="Stick"
         StartLocationOffset=(X=-20.000000)
         StartSizeRange=(X=(Min=4.000000,Max=4.000000),Y=(Min=25.000000,Max=25.000000),Z=(Min=25.000000,Max=25.000000))
         InitialParticlesPerSecond=1000.000000
         AutomaticInitialSpawning=False
         Texture=Texture'LineageEffectsTextures.Particles.fx_m_t0000'
         TextureUSubdivisions=8
         TextureVSubdivisions=4
         SubdivisionStart=4
         SubdivisionEnd=5
         UseRandomSubdivision=True
         LifetimeRange=(Min=2.000000,Max=2.000000)
         StartVelocityRange=(X=(Min=0.100000,Max=0.100000),Y=(Min=0.000000,Max=0.000000),Z=(Min=0.000000,Max=0.000000))
     End Object
     Emitters(1)=SpriteEmitter'SpriteEmitter9'
     AutoReset=True
     Tag=Emitter
     DrawScale=0.100000
}
