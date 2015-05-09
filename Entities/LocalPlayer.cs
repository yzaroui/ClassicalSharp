﻿using System;
using System.Drawing;
using ClassicalSharp.Renderers;
using OpenTK;
using OpenTK.Input;

namespace ClassicalSharp {
	
	public class LocalPlayer : Player {
		
		public Vector3 SpawnPoint;
		
		public float ReachDistance = 5f;
		
		public byte UserType;
		bool canSpeed = true, canFly = true, canRespawn = true, canNoclip = true;
		
		public bool CanSpeed {
			get { return canSpeed; }
			set { canSpeed = value; }
		}
		
		public bool CanFly {
			get { return canFly; }
			set { canFly = value; if( !value ) flying = false; }
		}
		
		public bool CanRespawn {
			get { return canRespawn; }
			set { canRespawn = value; }
		}
		
		public bool CanNoclip {
			get { return canNoclip; }
			set { canNoclip = value; if( !value ) noClip = false; }
		}
		
		float jumpVel = 0.42f;
		public float JumpHeight {
			get { return (float)GetMaxHeight( jumpVel ); }
		}
		
		public LocalPlayer( byte id, Game window ) : base( id, window ) {
			DisplayName = window.Username;
			SkinName = window.Username;
			map = window.Map;
		}
		
		public override void SetLocation( LocationUpdate update, bool interpolate ) {
			if( update.IncludesPosition ) {
				nextPos = update.RelativePosition ? nextPos + update.Pos : update.Pos;
				if( !interpolate ) {
					lastPos = Position = nextPos;
				}
			}
			if( update.IncludesOrientation ) {
				nextYaw = update.Yaw;
				nextPitch = update.Pitch;
				if( !interpolate ) {
					lastYaw = YawDegrees = nextYaw;
					lastPitch = PitchDegrees = nextPitch;
				}
			}
		}
		
		public override void Despawn() {
			if( renderer != null ) {
				renderer.Dispose();
			}
		}
		
		public override void Tick( double delta ) {
			if( Window.Map.IsNotLoaded ) return;
			//Window.Title = ( GC.GetTotalMemory( false ) / 1024.0 / 1024.0 ).ToString(); // TODO: temp debug statement
			
			float xMoving = 0, zMoving = 0;
			lastPos = Position = nextPos;
			lastYaw = nextYaw;
			lastPitch = nextPitch;
			HandleInput( ref xMoving, ref zMoving );
			UpdateVelocityYState();
			PhysicsTick( xMoving, zMoving );
			nextPos = Position;
			Position = lastPos;
			UpdateAnimState( lastPos, nextPos, delta );
			if( renderer != null ) {
				CheckSkin();
			}
		}
		
		public override void Render( double deltaTime, float t ) {
			if( !Window.Camera.IsThirdPerson ) return;
			if( renderer == null ) {
				renderer = new PlayerRenderer( this, Window );
				Window.AsyncDownloader.DownloadSkin( SkinName );
			}
			SetCurrentAnimState( t );
			renderer.Render( deltaTime );
		}
		
		void HandleInput( ref float xMoving, ref float zMoving ) {
			if( Window.ScreenLockedInput ) {
				jumping = speeding = flyingUp = flyingDown = false;
			} else {
				if( Window.IsKeyDown( KeyMapping.Forward ) ) xMoving -= 0.98f;
				if( Window.IsKeyDown( KeyMapping.Back ) ) xMoving += 0.98f;
				if( Window.IsKeyDown( KeyMapping.Left ) ) zMoving -= 0.98f;
				if( Window.IsKeyDown( KeyMapping.Right ) ) zMoving += 0.98f;

				jumping = Window.IsKeyDown( KeyMapping.Jump );
				speeding = canSpeed && Window.IsKeyDown( KeyMapping.Speed );
				flyingUp = Window.IsKeyDown( KeyMapping.FlyUp );
				flyingDown = Window.IsKeyDown( KeyMapping.FlyDown );
			}
		}
		
		void UpdateVelocityYState() {
			if( flying || noClip ) {
				Velocity.Y = 0; // eliminate the effect of gravity
				float vel = noClip ? 0.24f : 0.06f;
				float velSpeeding = noClip ? 0.48f : 0.08f;
				if( flyingUp || jumping ) {
					Velocity.Y = speeding ? velSpeeding : vel;
				} else if( flyingDown ) {
					Velocity.Y = speeding ? -velSpeeding : -vel;
				}
			} else if( jumping && TouchesAnyRope() && Velocity.Y > 0.02f ) {
				Velocity.Y = 0.02f;
			}

			if( jumping ) {
				if( TouchesAnyWater() || TouchesAnyLava() ) {
					Velocity.Y += speeding ? 0.08f : 0.04f;
				} else if( TouchesAnyRope() ) {
					Velocity.Y += speeding ? 0.15f : 0.10f;
				} else if( onGround ) {
					Velocity.Y = speeding ? jumpVel * 2 : jumpVel;
				}
			}
		}
		
		static Vector3 waterDrag = new Vector3( 0.8f, 0.8f, 0.8f ),
		lavaDrag = new Vector3( 0.5f, 0.5f, 0.5f ),
		ropeDrag = new Vector3( 0.5f, 0.85f, 0.5f ),
		normalDrag = new Vector3( 0.91f, 0.98f, 0.91f ),
		airDrag = new Vector3( 0.6f, 1f, 0.6f );
		const float liquidGrav = 0.02f, ropeGrav = 0.034f, normalGrav = 0.08f;
		void PhysicsTick( float xMoving, float zMoving ) {
			float multiply = flying ? ( speeding ? 90 : 15 ) : ( speeding ? 10 : 1 );

			if( TouchesAnyWater() && !flying && !noClip ) {
				Move( xMoving, zMoving, 0.02f * multiply, waterDrag, liquidGrav, 1 );
			} else if( TouchesAnyLava() && !flying && !noClip ) {
				Move( xMoving, zMoving, 0.02f * multiply, lavaDrag, liquidGrav, 1 );
			} else if( TouchesAnyRope() && !flying && !noClip ) {
				Move( xMoving, zMoving, 0.02f * 1.7f, ropeDrag, ropeGrav, 1 );
			} else {
				float factor = !flying && onGround ? 0.1f : 0.02f;
				float yMul = Math.Max( 1, multiply / 5f );		
				Move( xMoving, zMoving, factor * multiply, normalDrag, normalGrav, yMul );
				
				if( BlockUnderFeet == Block.Ice ) {
					Utils.Clamp( ref Velocity.X, -0.25f, 0.25f );
					Utils.Clamp( ref Velocity.Z, -0.25f, 0.25f );
				} else if( onGround || flying ) {
					Velocity *= airDrag; // air drag or ground friction
				}
			}
		}
		
		void AdjHeadingVelocity( float x, float z, float factor ) {
			float dist = (float)Math.Sqrt( x * x + z * z );
			if( dist < 0.00001f ) return;
			if( dist < 1 ) dist = 1;

			float multiply = factor / dist;
			x *= multiply;
			z *= multiply;
			float cosA = (float)Math.Cos( YawRadians );
			float sinA = (float)Math.Sin( YawRadians );
			Velocity.X += x * cosA - z * sinA;
			Velocity.Z += x * sinA + z * cosA;
		}
		
		void Move( float xMoving, float zMoving, float factor, Vector3 drag, float gravity, float yMul ) {
			AdjHeadingVelocity( zMoving, xMoving, factor );
			Velocity.Y *= yMul;
			if( !noClip ) 
				MoveAndWallSlide();
			Position += Velocity;
			
			Velocity.Y /= yMul;
			Velocity *= drag;
			Velocity.Y -= gravity;
		}
		
		bool jumping, speeding, flying, noClip, flyingDown, flyingUp;
		public void ParseHackFlags( string name, string motd ) {
			string joined = name + motd;
			if( joined.Contains( "-hax" ) ) {
				CanFly = CanNoclip = CanSpeed = CanRespawn = false;
				Window.CanUseThirdPersonCamera = false;
				Window.SetCamera( false );
			} else { // By default (this is also the case with WoM), we can use hacks.
				CanFly = CanNoclip = CanSpeed = CanRespawn = true;
				Window.CanUseThirdPersonCamera = true;
			}
			
			ParseFlag( b => CanFly = b, joined, "fly" );
			ParseFlag( b => CanNoclip = b, joined, "noclip" );
			ParseFlag( b => CanSpeed = b, joined, "speed" );
			ParseFlag( b => CanRespawn = b, joined, "respawn" );

			if( UserType == 0x64 ) {
				ParseFlag( b => CanFly = CanNoclip = CanRespawn = CanSpeed = b, joined, "ophax" );
			}
		}
		
		static void ParseFlag( Action<bool> action, string joined, string flag ) {
			if( joined.Contains( "+" + flag ) ) {
				action( true );
			} else if( joined.Contains( "-" + flag ) ) {
				action( false );
			}
		}
		
		Vector3 lastPos, nextPos;
		float lastYaw, nextYaw, lastPitch, nextPitch;
		public void SetInterpPosition( float t ) {
			Position = Vector3.Lerp( lastPos, nextPos, t );
			YawDegrees = Utils.Lerp( lastYaw, nextYaw, t );
			PitchDegrees = Utils.Lerp( lastPitch, nextPitch, t );
		}
		
		internal void HandleKeyDown( Key key ) {
			if( key == Window.Keys[KeyMapping.Respawn] && canRespawn ) {
				LocationUpdate update = LocationUpdate.MakePos( SpawnPoint, false );
				SetLocation( update, false );
			} else if( key == Window.Keys[KeyMapping.SetSpawn] && canRespawn ) {
				SpawnPoint = Position;
			} else if( key == Window.Keys[KeyMapping.Fly] && canFly ) {
				flying = !flying;
			} else if( key == Window.Keys[KeyMapping.NoClip] && canNoclip ) {
				noClip = !noClip;
			}
		}
		
		internal void CalculateJumpVelocity( float jumpHeight ) {
			jumpVel = 0;
			if( jumpHeight >= 256 ) jumpVel = 10.0f;
			if( jumpHeight >= 512 ) jumpVel = 16.5f;
			if( jumpHeight >= 768 ) jumpVel = 22.5f;
			
			while( GetMaxHeight( jumpVel ) <= jumpHeight ) {
				jumpVel += 0.01f;
			}
		}
		
		static double GetMaxHeight( float u ) {
			// equation below comes from solving diff(x(t, u))= 0
			// We only work in discrete timesteps, so test both rounded up and down.
			double t = 49.49831645 * Math.Log( 0.247483075 * u + 0.9899323 );
			return Math.Max( YPosAt( (int)t, u ), YPosAt( (int)t + 1, u ) );
		}
		
		static double YPosAt( int t, float u ) {
			// v(t, u) = (4 + u) * (0.98^t) - 4, where u = initial velocity
			// x(t, u) = Σv(t, u) from 0 to t (since we work in discrete timesteps)
			// plugging into Wolfram Alpha gives 1 equation as
			// (0.98^t) * (-49u - 196) - 4t + 50u + 196
			double a = Math.Exp( -0.0202027 * t ); //~0.98^t
			return a * ( -49 * u - 196 ) - 4 * t + 50 * u + 196;
		}
	}
}