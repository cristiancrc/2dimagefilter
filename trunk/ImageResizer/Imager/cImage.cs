﻿#region (c)2010 Hawkynt
/*
 *  cImage 
 *  Image filtering library 
    Copyright (C) 2010 Hawkynt

    This program is free software: you can redistribute it and/or modify
    it under the terms of the GNU General Public License as published by
    the Free Software Foundation, either version 3 of the License, or
    (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program.  If not, see <http://www.gnu.org/licenses/>.
 * This is a C# port of my former classImage perl library.
 * You can use and modify my code as long as you give me a credit and 
 * inform me about updates, changes new features and modification. 
 * Distribution and selling is allowed. Would be nice if you give some 
 * payback.
 * 
 * Mapping usually is implemented as
 *
 * 2x:
 * C0 C1 C2     00  01
 * C3 C4 C5 =>
 * C6 C7 C8     10  11
 * 
 * 3x:
 * C0 C1 C2    00 01 02
 * C3 C4 C5 => 10 11 12
 * C6 C7 C8    20 21 22
      
 */
#endregion
using System;
using System.Collections.Concurrent;
using System.Drawing;
using System.Drawing.Imaging;
using System.Linq;
using System.Threading.Tasks;
using nImager.Filters;

namespace nImager {
  /// <summary>
  /// A bitmap image
  /// </summary>
  public class cImage:ICloneable {
    #region helper structs
    /// <summary>
    /// A filter structure containing necessary fields used in filtering.
    /// </summary>
    public struct sFilter {
      /// <summary>
      /// The scale factor in X-direction.
      /// </summary>
      public byte ScaleX;
      /// <summary>
      /// The scale factor in Y-direction.
      /// </summary>
      public byte ScaleY;
      /// <summary>
      /// The name of the filter.
      /// </summary>
      public string Name;
      /// <summary>
      /// Additional parameters.
      /// </summary>
      public object Parameter;
      /// <summary>
      /// An action that filters a specified pixel from the source image into a destination area.
      /// </summary>
      public Action<cImage, int, int, cImage, int, int, byte, byte, object> FilterFunction;
      /// <summary>
      /// A function that takes an image and creates a new one based on that one.
      /// </summary>
      public Func<cImage, cImage> CreationFunction;
      /// <summary>
      /// Initializes a new instance of the <see cref="sFilter"/> struct.
      /// </summary>
      /// <param name="strName">Name of the filter.</param>
      /// <param name="ptrFunc">The function that creates the new image with the same dimensions.</param>
      public sFilter(string strName, Func<cImage, cImage> ptrFunc):this(strName) {
        this.CreationFunction = ptrFunc;
      }
      /// <summary>
      /// Initializes a new instance of the <see cref="sFilter"/> struct.
      /// </summary>
      /// <param name="strName">Name of the filter.</param>
      /// <param name="byteScaleX">The X-scale factor, defaults to <c>1</c>.</param>
      /// <param name="byteScaleY">The Y-scale factor, defaults to <c>1</c>.</param>
      /// <param name="ptrFilter">The filter function, defaults to <c>null</c>.</param>
      /// <param name="objParam">The additional parameters, default to <c>null</c>.</param>
      public sFilter(string strName, byte byteScaleX=1, byte byteScaleY=1, Action<cImage, int, int, cImage, int, int, byte, byte, object> ptrFilter=null, object objParam=null) {
        this.Name = strName;
        this.Parameter = objParam;
        this.ScaleX = byteScaleX;
        this.ScaleY = byteScaleY;
        this.FilterFunction = ptrFilter;
        this.CreationFunction=objSource=>new cImage(objSource.Width*byteScaleX,objSource.Height*byteScaleY);
      }
    }
    #endregion

    #region class fields
    /// <summary>
    /// available image filters
    /// </summary>
    private static readonly sFilter[] _arrFilters = new[]{
      /*
      new sFilter("-50% Scanlines",1,2,libBasic.voidHScanlines,-50f),
      new sFilter("+50% Scanlines",1,2,libBasic.voidHScanlines,50f),
      new sFilter("+100% Scanlines",1,2,libBasic.voidHScanlines,100f),
      new sFilter("-50% VScanlines",2,1,libBasic.voidVScanlines,-50f),
      new sFilter("+50% VScanlines",2,1,libBasic.voidVScanlines,50f),
      new sFilter("+100% VScanlines",2,1,libBasic.voidVScanlines,100f),
      new sFilter("MAME TV 2x",2,2,libMAME.voidTV2X),
      new sFilter("MAME TV 3x",3,3,libMAME.voidTV3X),
      new sFilter("MAME RGB 2x",2,2,libMAME.voidRGB2X),
      new sFilter("MAME RGB 3x",3,3,libMAME.voidRGB3X),
      new sFilter("Hawkynt TV 2x",2,2,libHawkynt.voidTV2X),
      new sFilter("Hawkynt TV 3x",3,3,libHawkynt.voidTV3X),
      */
      new sFilter("Bilinear Plus Original",2,2,libVBA.voidBilinearPlusOriginal),
      new sFilter("Bilinear Plus",2,2,libVBA.voidBilinearPlus),
      new sFilter("Eagle 2x",2,2,libEagle.voidEagle2x),
      new sFilter("Eagle 3x",3,3,libEagle.voidEagle3x),
      new sFilter("Eagle 3xB",3,3,libEagle.voidEagle3xB),
      new sFilter("Super Eagle",2,2,libKreed.voidSuperEagle),
      new sFilter("SaI 2x",2,2,libKreed.voidSaI2X),
      new sFilter("Super SaI",2,2,libKreed.voidSuperSaI),
      new sFilter("AdvInterp 2x",2,2,libMAME.voidAdvInterp2x),
      new sFilter("AdvInterp 3x",3,3,libMAME.voidAdvInterp3x),
      new sFilter("Scale 2x",2,2,libMAME.voidScale2x),
      new sFilter("Scale 3x",3,3,libMAME.voidScale3x),
      new sFilter("EPXB",2,2,libSNES9x.voidEPXB),
      new sFilter("EPXC",2,2,libSNES9x.voidEPXC),
      new sFilter("EPX3",3,3,libSNES9x.voidEPX3),
      new sFilter("HQ 2x",2,2,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrHQ2x)),
      new sFilter("HQ 2x3",2,3,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrHQ2x3)),
      new sFilter("HQ 2x4",2,4,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrHQ2x4)),
      new sFilter("HQ 3x",3,3,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrHQ3x)),
      new sFilter("HQ 4x",4,4,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrHQ4x)),
      new sFilter("HQ 2x Bold",2,2,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrHQ2x)),
      new sFilter("HQ 2x3 Bold",2,3,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrHQ2x3)),
      new sFilter("HQ 2x4 Bold",2,4,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrHQ2x4)),
      new sFilter("HQ 3x Bold",3,3,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrHQ3x)),
      new sFilter("HQ 4x Bold",4,4,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrHQ4x)),
      new sFilter("HQ 2x Smart",2,2,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrHQ2x)),
      new sFilter("HQ 2x3 Smart",2,3,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrHQ2x3)),
      new sFilter("HQ 2x4 Smart",2,4,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrHQ2x4)),
      new sFilter("HQ 3x Smart",3,3,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrHQ3x)),
      new sFilter("HQ 4x Smart",4,4,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrHQ4x)),
      new sFilter("LQ 2x",2,2,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrLQ2x)),
      new sFilter("LQ 2x3",2,3,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrLQ2x3)),
      new sFilter("LQ 2x4",2,4,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrLQ2x4)),
      new sFilter("LQ 3x",3,3,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrLQ3x)),
      new sFilter("LQ 4x",4,4,libHQ.voidComplex_nQwXh,new libHQ.delHQFilter(libHQ._arrLQ4x)),
      new sFilter("LQ 2x Bold",2,2,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrLQ2x)),
      new sFilter("LQ 2x3 Bold",2,3,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrLQ2x3)),
      new sFilter("LQ 2x4 Bold",2,4,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrLQ2x4)),
      new sFilter("LQ 3x Bold",3,3,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrLQ3x)),
      new sFilter("LQ 4x Bold",4,4,libHQ.voidComplex_nQwXhBold,new libHQ.delHQFilter(libHQ._arrLQ4x)),
      new sFilter("LQ 2x Smart",2,2,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrLQ2x)),
      new sFilter("LQ 2x3 Smart",2,3,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrLQ2x3)),
      new sFilter("LQ 2x4 Smart",2,4,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrLQ2x4)),
      new sFilter("LQ 3x Smart",3,3,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrLQ3x)),
      new sFilter("LQ 4x Smart",4,4,libHQ.voidComplex_nQwXhSmart,new libHQ.delHQFilter(libHQ._arrLQ4x)),
      
      new sFilter("Red",objS=>objS.R),
      new sFilter("Green",objS=>objS.G),
      new sFilter("Blue",objS=>objS.B),
      new sFilter("Y",objS=>objS.Y),
      new sFilter("U",objS=>objS.U),
      new sFilter("V",objS=>objS.V),
      new sFilter("u",objS=>objS.u),
      new sFilter("v",objS=>objS.v),
      new sFilter("Hue",objS=>objS.Hue),
      new sFilter("Hue Colored",objS=>objS.HueColored),
      new sFilter("Brightness",objS=>objS.Brightness),
      new sFilter("Min",objS=>objS.Min),
      new sFilter("Max",objS=>objS.Max)
    };
    #endregion

    // image data
    /// <summary>
    /// An array containing the images' pixel data
    /// </summary>
    private readonly sPixel[] _arrImageData;
    /// <summary>
    /// The images' width
    /// </summary>
    private readonly int _intWidth = 0;
    /// <summary>
    /// The images' height
    /// </summary>
    private readonly int _intHeight = 0;

    #region properties
    /// <summary>
    /// Gets the available image filters.
    /// </summary>
    /// <value>The filters.</value>
    public static sFilter[] Filters {
      get {
        return (cImage._arrFilters.ToArray());
      }
    }
    /// <summary>
    /// Gets the width of the image.
    /// </summary>
    /// <value>The width.</value>
    public int Width {
      get {
        return (this._intWidth);
      }
    }
    /// <summary>
    /// Gets the height of the image.
    /// </summary>
    /// <value>The height.</value>
    public int Height {
      get {
        return (this._intHeight);
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the red values only.
    /// </summary>
    /// <value>The greyscale image from the red components.</value>
    public cImage R {
      get {
        return (new cImage(this, stA => stA.R));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the green values only.
    /// </summary>
    /// <value>The greyscale image from the green components.</value>
    public cImage G {
      get {
        return (new cImage(this, stA => stA.G));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the blue values only.
    /// </summary>
    /// <value>The greyscale image from the blue components.</value>
    public cImage B {
      get {
        return (new cImage(this, stA => stA.B));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the luminance values only.
    /// </summary>
    /// <value>The greyscale image from the luminance components.</value>
    public cImage Y {
      get {
        return (new cImage(this, stA => stA.Y));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the color(U) values only.
    /// </summary>
    /// <value>The greyscale image from the color(U) components.</value>
    public cImage U {
      get {
        return (new cImage(this, stA => stA.U));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the color(V) values only.
    /// </summary>
    /// <value>The greyscale image from the color(V) components.</value>
    public cImage V {
      get {
        return (new cImage(this, stA => stA.V));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the color(u) values only.
    /// </summary>
    /// <value>The greyscale image from the color(u) components.</value>
    public cImage u {
      get {
        return (new cImage(this, stA => stA.u));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the color(v) values only.
    /// </summary>
    /// <value>The greyscale image from the color(v) components.</value>
    public cImage v {
      get {
        return (new cImage(this, stA => stA.v));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the brightness values only.
    /// </summary>
    /// <value>The greyscale image from the brightness components.</value>
    public cImage Brightness {
      get {
        return (new cImage(this, stA => stA.Brightness));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the minimum values only.
    /// </summary>
    /// <value>The greyscale image from the minimum of all components.</value>
    public cImage Min {
      get {
        return (new cImage(this, stA => stA.Min));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the maximum values only.
    /// </summary>
    /// <value>The greyscale image from the maximum of all components.</value>
    public cImage Max {
      get {
        return (new cImage(this, stA => stA.Max));
      }
    }
    /// <summary>
    /// Gets the a new instance containing a greyscale image of the hue values only.
    /// </summary>
    /// <value>The greyscale image from the hue components.</value>
    public cImage Hue {
      get {
        return (new cImage(this, stA => stA.Hue));
      }
    }
    /// <summary>
    /// Gets the a new instance containing an image of the hue values only.
    /// </summary>
    /// <value>The image from the hue components.</value>
    public cImage HueColored {
      get {
        return (new cImage(this, stA => {
          byte byteR, byteG, byteB;
          float fltHue = stA.Hue * 360f / 256f;
          float fltFactor = 255f / 120f;
          byteR = (byte)((fltHue <= 120) ? 255 - fltHue * fltFactor : (fltHue >= 240) ? (fltHue - 240) * fltFactor : 0);
          byteG = (byte)((fltHue <= 120) ? fltHue * fltFactor : (fltHue <= 240) ? 255 - (fltHue - 120) * fltFactor : 0);
          byteB = (byte)((fltHue >= 240) ? 255 - (fltHue - 240) * fltFactor : (fltHue >= 120) ? (fltHue - 120) * fltFactor : 0);
          return (new sPixel(byteR, byteG, byteB));
        }));
      }
    }
    #endregion
    #region ctor dtor idx
    // NOTE: Bitmap objects does not support parallel read-outs blame Microsoft
    /// <summary>
    /// Initializes a new instance of the <see cref="cImage"/> class from a <see cref="Bitmap"/> instance.
    /// </summary>
    /// <param name="objBitmap">The bitmap.</param>
    public cImage(Bitmap objBitmap)
      : this(objBitmap != null ? objBitmap.Width : 0, objBitmap != null ? objBitmap.Height : 0) {
      if (objBitmap == null) return;
      BitmapData objBitmapData = objBitmap.LockBits(
        new Rectangle(0, 0, this._intWidth, this._intHeight),
        ImageLockMode.ReadOnly,
        PixelFormat.Format24bppRgb
      );
      int intFillX = objBitmapData.Stride - objBitmapData.Width * 3;
      unsafe {
        byte* ptrOffset = (byte*)objBitmapData.Scan0.ToPointer();
        for (int intY = 0; intY < this._intHeight; intY++) {
          for (int intX = 0; intX < this._intWidth; intX++) {
            this[intX, intY] = new sPixel(*(ptrOffset+2), *(ptrOffset + 1), *(ptrOffset + 0));
            ptrOffset += 3;
          }
          ptrOffset += intFillX;
        }
      }
      objBitmap.UnlockBits(objBitmapData);
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="cImage"/> class.
    /// </summary>
    /// <param name="intWidth">Width of the image.</param>
    /// <param name="intHeight">Height of the image.</param>
    public cImage(int intWidth, int intHeight) {
      this._intWidth = intWidth;
      this._intHeight = intHeight;
      this._arrImageData = new sPixel[intWidth * intHeight];
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="cImage"/> class from a given one.
    /// </summary>
    /// <param name="objSource">The source image.</param>
    public cImage(cImage objSource):this(objSource._intWidth,objSource._intHeight) {
      for (long lngI = 0; lngI < objSource._arrImageData.LongLength; lngI++)
        this._arrImageData[lngI] = objSource._arrImageData[lngI];
    }
    /// <summary>
    /// Initializes a new instance of the <see cref="cImage"/> class by filtering a given one.
    /// </summary>
    /// <param name="objSource">The source image.</param>
    /// <param name="ptrFilter">The filter.</param>
    public cImage(cImage objSource, Func<sPixel, sPixel> ptrFilter) {
      this._intWidth = objSource._intWidth;
      this._intHeight = objSource._intHeight;
      this._arrImageData = new sPixel[objSource._arrImageData.LongLength];
      Parallel.ForEach(Partitioner.Create(0, this._intHeight), () => 0, (objRange, objParallelState, objThreadStorage) => {
        for (int intY = objRange.Item1; intY < objRange.Item2; intY++)
          for (int intX = 0; intX < this._intWidth; intX++)
            this[intX, intY] = ptrFilter(objSource[intX, intY]);
        return (objThreadStorage);
      }, objFinalLocal => {
      });
    }
    /// <summary>
    /// Initializes a new greyscale instance of the <see cref="cImage"/> class by filtering a given one.
    /// </summary>
    /// <param name="objSource">The source image.</param>
    /// <param name="ptrFilter">The greyscale filter.</param>
    public cImage(cImage objSource, Func<sPixel, byte> ptrFilter) {
      this._intWidth = objSource._intWidth;
      this._intHeight = objSource._intHeight;
      this._arrImageData = new sPixel[objSource._arrImageData.LongLength];
      Parallel.ForEach(Partitioner.Create(0, this._intHeight), () => 0, (objRange, objParallelState, objThreadStorage) => {
        for (int intY = objRange.Item1; intY < objRange.Item2; intY++)
          for (int intX = 0; intX < this._intWidth; intX++) {
            byte byteD = ptrFilter(objSource[intX, intY]);
            this[intX, intY] = new sPixel(byteD, byteD, byteD);
          }
        return (objThreadStorage);
      }, objFinalLocal => {
      });
    }
    /// <summary>
    /// Gets or sets the <see cref="nImager.sPixel"/> with the specified X, Y coordinates.
    /// </summary>
    /// <value>The pixel</value>
    public sPixel this[int intX, int intY] {
      get {
        if (intX < 0)
          intX = 0;
        if (intY < 0)
          intY = 0;
        if (intX >= this._intWidth)
          intX = this._intWidth - 1;
        if (intY >= this._intHeight)
          intY = this._intHeight - 1;
        
        return (this._arrImageData[intY * this._intWidth + intX]);
      }
      set {
        if(intX<this._intWidth && intY<this._intHeight && intX>=0 && intY>=0)
          this._arrImageData[intY * this._intWidth + intX] = value;
      }
    }
    #endregion
    #region generic image filter
    /// <summary>
    /// Filters this image by using a given filter structure.
    /// </summary>
    /// <param name="stFilter">The filter.</param>
    /// <returns>A new instance containing the filtered image.</returns>
    private cImage _objFilterImage(sFilter stFilter) {
      cImage objRet = stFilter.CreationFunction(this);
      if (stFilter.FilterFunction != null) {
        //Parallel.For(0, this._intHeight, intSrcY => {
        Parallel.ForEach(
          Partitioner.Create(0, this._intHeight),
          () => 0,
          (objRange, objParallelState, objThreadStorage) => {
            for (int intSrcY = objRange.Item1; intSrcY < objRange.Item2; intSrcY++)
              for (int intSrcX = 0; intSrcX < this._intWidth; intSrcX++) {
                stFilter.FilterFunction(this, intSrcX, intSrcY, objRet, intSrcX * stFilter.ScaleX, intSrcY * stFilter.ScaleY, stFilter.ScaleX, stFilter.ScaleY, stFilter.Parameter);
              }
            return (objThreadStorage);
          },
          objFinalLocal => {
          }
        );
      }
      return (objRet);
    }
    /// <summary>
    /// Filters the current image using a named filter.
    /// </summary>
    /// <param name="strFilter">The name of the filter.</param>
    /// <returns>A new instance containing the filtered image or <c>null</c>, if the specified filter could not be found.</returns>
    public cImage FilterImage(string strFilter) {
      cImage objRet = null;
      strFilter = strFilter.ToLower();
      sFilter stFilter = default(sFilter);
      lock (cImage._arrFilters)
        for (int intI = 0; intI < cImage._arrFilters.Length && stFilter.FilterFunction == null; intI++)
          if (cImage._arrFilters[intI].Name.ToLower() == strFilter)
            stFilter = cImage._arrFilters[intI];
      if (stFilter.FilterFunction != null || stFilter.CreationFunction!=null)
        objRet = this._objFilterImage(stFilter);
      return (objRet);
    }
    #endregion
    /// <summary>
    /// Converts this image to a <see cref="Bitmap"/> instance.
    /// </summary>
    /// <returns>The <see cref="Bitmap"/> instance</returns>
    public Bitmap ToBitmap() {
      Bitmap objRet = new Bitmap(this.Width, this.Height);
      // NOTE: fucking bitmap does not allow parallel writes
      BitmapData objBitmapData = objRet.LockBits(
        new Rectangle(0, 0, objRet.Width, objRet.Height),
        ImageLockMode.WriteOnly,
        PixelFormat.Format24bppRgb
      );
      int intFillX = objBitmapData.Stride - objBitmapData.Width * 3;
      unsafe {
        byte* ptrOffset = (byte*)objBitmapData.Scan0.ToPointer();
        for (int intY = 0; intY < this._intHeight; intY++) {
          for (int intX = 0; intX < this._intWidth; intX++) {
            *(ptrOffset+0) = this[intX, intY].B;
            *(ptrOffset+1) = this[intX, intY].G;
            *(ptrOffset+2) = this[intX, intY].R;
            ptrOffset += 3;
          }
          ptrOffset += intFillX;
        }
      }
      objRet.UnlockBits(objBitmapData);
      return (objRet);
    }
    /// <summary>
    /// Fills the image with the specified color.
    /// </summary>
    /// <param name="byteR">The red-value.</param>
    /// <param name="byteG">The green-value.</param>
    /// <param name="byteB">The blue-value.</param>
    public void Fill(byte byteR, byte byteG, byte byteB) {
      this.Fill(new sPixel(byteR, byteG, byteB));
    }
    /// <summary>
    /// Fills the image with the specified pixel.
    /// </summary>
    /// <param name="stPixel">The pixel instance.</param>
    public void Fill(sPixel stPixel) {
      Parallel.For(0, this._arrImageData.LongLength, qwordOffset => this._arrImageData[qwordOffset] = stPixel);
    }

    #region ICloneable Members
    /// <summary>
    /// Creates a new object that is a copy of the current instance.
    /// </summary>
    /// <returns>
    /// A new object that is a copy of this instance.
    /// </returns>
    public object Clone() {
      return (new cImage(this));
    }
    #endregion
  }
}
