// 
// MacErrorDialogHandler.cs
//  
// Author:
//       Alan McGovern <alan@xamarin.com>
// 
// Copyright 2011 Xamarin, Inc.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.

using System;
using System.Drawing;
using System.Linq;
using System.Collections.Generic;
using MonoMac.Foundation;
using MonoMac.AppKit;

using MonoDevelop.Core;
using MonoDevelop.Ide;
using MonoDevelop.Ide.Extensions;
using MonoDevelop.Components.Extensions;
using MonoDevelop.MacInterop;
	
namespace MonoDevelop.MacIntegration
{
	class MacExceptionDialogHandler : IExceptionDialogHandler
	{
		class MyTextView : NSTextView
		{
			public MyTextView (RectangleF frame)
				: base (frame)
			{

			}

			public override void KeyDown (NSEvent theEvent)
			{
				if (theEvent.ModifierFlags.HasFlag (NSEventModifierMask.CommandKeyMask)) {
					switch (theEvent.Characters) {
					case "x":
						Cut (this);
						break;
					case "c":
						NSRange range = SelectedRange;
						if (range.Length == 0)
							SelectAll (this);
						Copy (this);
						break;
					case "a":
						SelectAll (this);
						break;
					}
				}
				
				base.KeyDown (theEvent);
			}
		}
		
		public bool Run (ExceptionDialogData data)
		{
			using (var alert = new NSAlert { AlertStyle = NSAlertStyle.Critical }) {
				alert.Icon = NSApplication.SharedApplication.ApplicationIconImage;
				
				alert.MessageText = data.Title ?? GettextCatalog.GetString ("Error");
				
				if (!string.IsNullOrEmpty (data.Message)) {
					alert.InformativeText = data.Message;
				}

				List<AlertButton> buttons = null;
				if (data.Buttons != null && data.Buttons.Length > 0)
					buttons = data.Buttons.Reverse ().ToList ();

				if (buttons != null) {
					foreach (var button in buttons) {
						var label = button.Label;
						if (button.IsStockButton)
							label = Gtk.Stock.Lookup (label).Label;
						label = label.Replace ("_", "");

						//this message seems to be a standard Mac message since alert handles it specially
						if (button == AlertButton.CloseWithoutSave)
							label = GettextCatalog.GetString ("Don't Save");

						alert.AddButton (label);
					}
				}

				if (data.Exception != null) {
					var scrollSize = new SizeF (400, 130);
					float spacing = 4;
					
					string title = GettextCatalog.GetString ("View details");
					string altTitle = GettextCatalog.GetString ("Hide details");
					
					var buttonFrame = new RectangleF (0, 0, 0, 0);
					var button = new NSButton (buttonFrame) {
						BezelStyle = NSBezelStyle.Disclosure,
						Title = "",
						AlternateTitle = "",
					};
					button.SetButtonType (NSButtonType.OnOff);
					button.SizeToFit ();
					
					var label = new MDClickableLabel (title) {
						Alignment = NSTextAlignment.Left,
					};
					label.SizeToFit ();
					
					button.SetFrameSize (new SizeF (button.Frame.Width, Math.Max (button.Frame.Height, label.Frame.Height)));
					label.SetFrameOrigin (new PointF (button.Frame.Width + 5, button.Frame.Y));
					
					var text = new MyTextView (new RectangleF (0, 0, float.MaxValue, float.MaxValue)) {
						HorizontallyResizable = true,
					};
					text.TextContainer.ContainerSize = new SizeF (float.MaxValue, float.MaxValue);
					text.TextContainer.WidthTracksTextView = true;
					text.InsertText (new NSString (data.Exception.ToString ()));
					text.Editable = false;

					var scrollView = new NSScrollView (new RectangleF (PointF.Empty, SizeF.Empty)) {
						HasHorizontalScroller = true,
						HasVerticalScroller = true,
					};
					
					var accessory = new NSView (new RectangleF (0, 0, scrollSize.Width, button.Frame.Height));
					accessory.AddSubview (scrollView);
					accessory.AddSubview (button);
					accessory.AddSubview (label);
					
					alert.AccessoryView = accessory;
					
					button.Activated += delegate {
						float change;
						if (button.State == NSCellStateValue.On) {
							change = scrollSize.Height + spacing;
							label.StringValue = altTitle;
							scrollView.Hidden = false;
							scrollView.Frame = new RectangleF (PointF.Empty, scrollSize);
							scrollView.DocumentView = text;
						} else {
							change = -(scrollSize.Height + spacing);
							label.StringValue = title;
							scrollView.Hidden = true;
							scrollView.Frame = new RectangleF (PointF.Empty, SizeF.Empty);
						}
						var f = accessory.Frame;
						f.Height += change;
						accessory.Frame = f;
						var lf = label.Frame;
						lf.Y += change;
						label.Frame = lf;
						var bf = button.Frame;
						bf.Y += change;
						button.Frame = bf;
						label.SizeToFit ();
						var panel = (NSPanel) alert.Window;
						var pf = panel.Frame;
						pf.Height += change;
						pf.Y -= change;
						panel.SetFrame (pf, true, true);
						//unless we assign the icon again, it starts nesting old icon into the warning icon
						alert.Icon = NSApplication.SharedApplication.ApplicationIconImage;
						alert.Layout ();
					};
					label.OnMouseUp += (sender, e) => button.PerformClick (e.Event);
				}

				int result = alert.RunModal () - (int)NSAlertButtonReturn.First;
				data.ResultButton = buttons != null ? buttons [result] : null;
				GtkQuartz.FocusWindow (data.TransientFor ?? MessageService.RootWindow);
			}
			
			return true;
		}
		
		class MDClickableLabel: MDLabel
		{
			public MDClickableLabel (string text) : base (text)
			{
			}
			
			public override void MouseDown (NSEvent theEvent)
			{
				if (OnMouseDown != null)
					OnMouseDown (this, new NSEventArgs (theEvent));
				else
					base.MouseDown (theEvent);
			}
			
			public event EventHandler<NSEventArgs> OnMouseDown;
			
			public override void MouseUp (NSEvent theEvent)
			{
				if (OnMouseUp != null)
					OnMouseUp (this, new NSEventArgs (theEvent));
				else
					base.MouseUp (theEvent);
			}
			
			public event EventHandler<NSEventArgs> OnMouseUp;
			
			public override void MouseEntered (NSEvent theEvent)
			{
				if (OnMouseEntered != null)
					OnMouseEntered (this, new NSEventArgs (theEvent));
				else
					base.MouseEntered (theEvent);
			}
			
			public event EventHandler<NSEventArgs> OnMouseEntered;
			
			public override void MouseExited (NSEvent theEvent)
			{
				if (OnMouseExited != null)
					OnMouseExited (this, new NSEventArgs (theEvent));
				else
					base.MouseExited (theEvent);
			}
			
			public event EventHandler<NSEventArgs> OnMouseExited;
			
			public override void MouseMoved (NSEvent theEvent)
			{
				if (OnMouseMoved != null)
					OnMouseMoved (this, new NSEventArgs (theEvent));
				else
					base.MouseMoved (theEvent);
			}
			
			public event EventHandler<NSEventArgs> OnMouseMoved;
		}
		
		class NSEventArgs : EventArgs
		{
			public NSEventArgs (NSEvent evt)
			{
				this.Event = evt;
			}
			
			public NSEvent Event { get; private set; }
		}
	}
}