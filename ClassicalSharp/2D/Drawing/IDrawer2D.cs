﻿using System;
using System.Collections.Generic;
using System.Drawing;
using ClassicalSharp.GraphicsAPI;

namespace ClassicalSharp {

	/// <summary> Class responsibe for performing drawing operations on bitmaps
	/// and for converting bitmaps into graphics api textures. </summary>
	/// <remarks> Uses GDI+ on Windows, uses Cairo on Mono. </remarks>
	public abstract class IDrawer2D : IDisposable {
		
		protected IGraphicsApi graphics;
		public const float Offset = 1.3f;
		
		/// <summary> Sets the underlying bitmap that drawing operations will be performed on. </summary>
		public abstract void SetBitmap( Bitmap bmp );
		
		/// <summary> Draws a string using the specified arguments and fonts at the
		/// specified coordinates in the currently bound bitmap. </summary>
		public abstract void DrawText( ref DrawTextArgs args, float x, float y );
		
		/// <summary> Draws a 2D flat rectangle of the specified dimensions at the
		/// specified coordinates in the currently bound bitmap. </summary>
		public abstract void DrawRect( Color colour, int x, int y, int width, int height );
		
		/// <summary> Draws the outline of a 2D flat rectangle of the specified dimensions
		/// at the specified coordinates in the currently bound bitmap. </summary>
		public abstract void DrawRectBounds( Color colour, float lineWidth, int x, int y, int width, int height );
		
		/// <summary> Draws a 2D rectangle with rounded borders of the specified dimensions
		/// at the specified coordinates in the currently bound bitmap. </summary>
		public abstract void DrawRoundedRect( Color colour, float radius, float x, float y, float width, float height );
		
		/// <summary> Clears the entire bound bitmap to the specified colour. </summary>
		public abstract void Clear( Color colour );
		
		/// <summary> Clears the entire bound bitmap to the specified colour. </summary>
		public abstract void Clear( Color colour, int x, int y, int width, int height );
		
		/// <summary> Disposes of any resources used by this class that are associated with the underlying bitmap. </summary>
		public abstract void Dispose();
		
		/// <summary> Returns a new bitmap that has 32-bpp pixel format. </summary>
		public abstract Bitmap ConvertTo32Bpp( Bitmap src );
		
		/// <summary> Returns the size of a bitmap needed to contain the specified text with the given arguments. </summary>
		public abstract Size MeasureSize( ref DrawTextArgs args );
		
		/// <summary> Disposes of all native resources used by this class. </summary>
		/// <remarks> You will no longer be able to perform measuring or drawing calls after this. </remarks>
		public abstract void DisposeInstance();
		
		/// <summary> Draws the specified string from the arguments into a new bitmap,
		/// them creates a 2D texture with origin at the specified window coordinates. </summary>
		public Texture MakeTextTexture( ref DrawTextArgs args, int windowX, int windowY ) {
			Size size = MeasureSize( ref args );
			if( parts.Count == 0 )
				return new Texture( -1, windowX, windowY, 0, 0, 1, 1 );
			
			using( Bitmap bmp = CreatePow2Bitmap( size ) ) {
				SetBitmap( bmp );
				args.SkipPartsCheck = true;	
				
				DrawText( ref args, 0, 0 );
				Dispose();
				return Make2DTexture( bmp, size, windowX, windowY );
			}
		}
		
		/// <summary> Creates a 2D texture with origin at the specified window coordinates. </summary>
		public Texture Make2DTexture( Bitmap bmp, Size used, int windowX, int windowY ) {
			int texId = graphics.CreateTexture( bmp );
			return new Texture( texId, windowX, windowY, used.Width, used.Height,
			                   (float)used.Width / bmp.Width, (float)used.Height / bmp.Height );
		}
		
		/// <summary> Creates a power-of-2 sized bitmap larger or equal to to the given size. </summary>
		public static Bitmap CreatePow2Bitmap( Size size ) {
			return new Bitmap( Utils.NextPowerOf2( size.Width ), Utils.NextPowerOf2( size.Height ) );
		}
		
		protected List<TextPart> parts = new List<TextPart>( 64 );
		protected static Color white = Color.FromArgb( 255, 255, 255 );
		protected struct TextPart {
			public string Text;
			public Color TextColour;
			
			public TextPart( string text, Color col ) {
				Text = text;
				TextColour = col;
			}
		}
		
		protected void GetTextParts( string value ) {
			parts.Clear();
			if( String.IsNullOrEmpty( value ) ) {
			} else if( value.IndexOf( '&' ) == -1 ) {
				parts.Add( new TextPart( value, white ) );
			} else {
				SplitText( value );
			}
		}
		
		protected void SplitText( string value ) {
			int code = 0xF;
			for( int i = 0; i < value.Length; i++ ) {
				int nextAnd = value.IndexOf( '&', i );
				int partLength = nextAnd == -1 ? value.Length - i : nextAnd - i;
				
				if( partLength > 0 ) {
					string part = value.Substring( i, partLength );
					Color col = Color.FromArgb(
						191 * ((code >> 2) & 1) + 64 * (code >> 3),
						191 * ((code >> 1) & 1) + 64 * (code >> 3),
						191 * ((code >> 0) & 1) + 64 * (code >> 3) );
					parts.Add( new TextPart( part, col ) );
				}
				i += partLength + 1;
				
				if( nextAnd >= 0 && nextAnd + 1 < value.Length ) {
					try {
						code = Utils.ParseHex( value[nextAnd + 1] );
					} catch( FormatException ) {
						code = 0xF;
						i--; // include the character that isn't a colour code.
					}
				}
			}
		}
	}
}