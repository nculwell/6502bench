﻿/*
 * Copyright 2019 faddenSoft
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace CommonWPF
{
    /// <summary>
    /// Generic Visual helper.
    /// </summary>
    public static class VisualHelper
    {
        /// <summary>
        /// Find a child object in a WPF visual tree.
        /// </summary>
        /// <remarks>
        /// Sample usage:
        ///   GridViewHeaderRowPresenter headerRow = listView.GetVisualChild&lt;GridViewHeaderRowPresenter&gt;();
        ///   
        /// From https://social.msdn.microsoft.com/Forums/vstudio/en-US/7d0626cb-67e8-4a09-a01e-8e56ee7411b2/gridviewcolumheader-radiobuttons?forum=wpf
        /// </remarks>
        /// <typeparam name="T"></typeparam>
        /// <param name="referenceVisual">Start point.</param>
        /// <returns>Object of appropriate type, or null if not found.</returns>
        public static T? GetVisualChild<T>(this Visual referenceVisual) where T : Visual
        {
            Visual? child = null;
            for (Int32 i = 0; i < VisualTreeHelper.GetChildrenCount(referenceVisual); i++)
            {
                child = VisualTreeHelper.GetChild(referenceVisual, i) as Visual;
                if (child != null && child is T)
                {
                    break;
                }
                else if (child != null)
                {
                    child = GetVisualChild<T>(child);
                    if (child != null && child is T)
                    {
                        break;
                    }
                }
            }
            return child as T;
        }
    }

    /// <summary>
    /// Helper functions for working with a ListView.
    /// 
    /// ListViews are generalized to an absurd degree, so simple things like "what column did
    /// I click on" and "what row is at the top" that were easy in WinForms are not provided
    /// by WPF.
    /// </summary>
    public static class ListViewExtensions
    {
        /// <summary>
        /// Figures out which item index is at the top of the window.  This only works for a
        /// ListView with a VirtualizingStackPanel.
        /// </summary>
        /// <remarks>
        /// See https://stackoverflow.com/q/2926722/294248 for an alternative approach that
        /// uses hit-testing, as well as a copy of this approach.
        ///
        /// Looks like we get the same values from ScrollViewer.VerticalOffset.  I don't know
        /// if there's a reason to favor one over the other.
        /// </remarks>
        /// <returns>The item index, or -1 if the list is empty.</returns>
        public static int GetTopItemIndex(this ListView lv)
        {
            if (lv.Items.Count == 0)
            {
                return -1;
            }

            VirtualizingStackPanel? vsp = lv.GetVisualChild<VirtualizingStackPanel>();
            if (vsp == null)
            {
                Debug.Assert(false, "ListView does not have a VirtualizingStackPanel");
                return -1;
            }
            return (int)vsp.VerticalOffset;
        }

        /// <summary>
        /// Scrolls the ListView so that the specified item is at the top.  The standard
        /// ListView.ScrollIntoView() makes the item visible but doesn't ensure a
        /// specific placement.
        /// </summary>
        /// <remarks>
        /// Equivalent to setting myListView.TopItem in WinForms.  Unfortunately, the
        /// ScrollIntoView call takes 60-100ms on a list with fewer than 1,000 items.  And
        /// sometimes it just silently fails.  Prefer ScrollToIndex() to this.
        /// </remarks>
        public static void ScrollToTopItem(this ListView lv, object item)
        {
            ScrollViewer? sv = lv.GetVisualChild<ScrollViewer>();
            if (sv == null)
                throw new InvalidOperationException("ListView does not have a ScrollViewer");
            sv.ScrollToBottom();
            lv.ScrollIntoView(item);
        }

        /// <summary>
        /// Scrolls the ListView to the specified vertical index.  The ScrollViewer should
        /// be operating in "logical" units (lines) rather than "physical" units (pixels).
        /// </summary>
        public static void ScrollToIndex(this ListView lv, int index)
        {
            ScrollViewer? sv = lv.GetVisualChild<ScrollViewer>();
            if (sv == null)
                throw new InvalidOperationException("ListView does not have a ScrollViewer");
            sv.ScrollToVerticalOffset(index);
        }

        /// <summary>
        /// Returns the ListViewItem that was clicked on, or null if an LVI wasn't the target
        /// of a click (e.g. off the bottom of the list).
        /// </summary>
        public static ListViewItem? GetClickedItem(this ListView lv, MouseButtonEventArgs e)
        {
            DependencyObject dep = (DependencyObject)e.OriginalSource;

            // Should start at something like a TextBlock.  Walk up the tree until we hit the
            // ListViewItem.
            while (dep != null && dep is not ListViewItem)
            {
                dep = VisualTreeHelper.GetParent(dep);
            }
            if (dep == null)
            {
                return null;
            }
            return (ListViewItem)dep;
        }

        /// <summary>
        /// Determines which column was the target of a mouse click.  Only works for ListView
        /// with GridView.
        /// </summary>
        /// <remarks>
        /// There's just no other way to do this with ListView.  With DataGrid you can do this
        /// somewhat reasonably (see below), but ListView just doesn't want to help.
        /// </remarks>
        /// <returns>Column index, or -1 if the click was outside the columns (e.g. off the right
        ///   edge).</returns>
        public static int GetClickEventColumn(this ListView lv, MouseButtonEventArgs e)
        {
            // There's a bit of padding that seems to offset things.  Not sure how to account
            // for it, so for now just fudge it.
            const int FUDGE = 4;

            // Need to take horizontal scrolling into account.
            ScrollViewer? sv = lv.GetVisualChild<ScrollViewer>();
            if (sv == null)
                throw new InvalidOperationException("ListView does not have a ScrollViewer");
            double scrollPos = sv.HorizontalOffset;

            Point p = e.GetPosition(lv);
            GridView gv = (GridView)lv.View;
            double startPos = FUDGE - scrollPos;
            for (int index = 0; index < gv.Columns.Count; index++)
            {
                GridViewColumn col = gv.Columns[index];

                if (p.X < startPos + col.ActualWidth)
                {
                    return index;
                }
                startPos += col.ActualWidth;
            }

            return -1;
        }
    }

    /// <summary>
    /// Helper functions for working with DataGrids.
    /// </summary>
    /// <remarks>
    /// It's tempting to handle double-click actions by using the selected row.  This gets a
    /// little weird, though, because double-clicking on a header or blank area doesn't
    /// clear the selection.
    /// </remarks>
    public static class DataGridExtensions
    {
        /// <summary>
        /// Determines which row and column was the target of a mouse button action.
        /// </summary>
        /// <remarks>
        /// Based on https://blog.scottlogic.com/2008/12/02/wpf-datagrid-detecting-clicked-cell-and-row.html
        /// </remarks>
        /// <returns>True if the click was on a data item.</returns>
        public static bool GetClickRowColItem(this DataGrid dg, MouseButtonEventArgs e,
                out int rowIndex, out int colIndex, out object item)
        {
            rowIndex = colIndex = -1;
            item = new object(); // dummy object to return on false

            DependencyObject dep = (DependencyObject)e.OriginalSource;

            // The initial dep will likely be a TextBlock.  Walk up the tree until we find
            // an object for the cell.  If we don't find one, this might be a click in the
            // header or off the bottom of the list.
            while (dep is not DataGridCell)
            {
                dep = VisualTreeHelper.GetParent(dep);
                if (dep == null)
                {
                    return false;
                }
            }
            DataGridCell cell = (DataGridCell)dep;

            // Now search up for the DataGridRow object.
            do
            {
                dep = VisualTreeHelper.GetParent(dep);
                if (dep == null)
                {
                    Debug.Assert(false, "Found cell but not row?");
                    return false;
                }
            } while (!(dep is DataGridRow));
            DataGridRow row = (DataGridRow)dep;

            // Get a row index for the entry.
            DataGrid rowGrid = (DataGrid)ItemsControl.ItemsControlFromItemContainer(row);
            rowIndex = rowGrid.ItemContainerGenerator.IndexFromContainer(row);

            // Column index is, weirdly enough, just sitting in a property.
            colIndex = cell.Column.DisplayIndex;

            // Item is part of the row.
            item = row.Item;
            return true;
        }

#if false
        public static DataGridRow GetRow(this DataGrid grid, int index) {
            DataGridRow row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
            if (row == null) {
                // May be virtualized, bring into view and try again.
                grid.UpdateLayout();
                grid.ScrollIntoView(grid.Items[index]);
                row = (DataGridRow)grid.ItemContainerGenerator.ContainerFromIndex(index);
            }
            return row;
        }
#endif
    }

    /// <summary>
    /// RichTextBox extensions.
    /// </summary>
    public static class RichTextBoxExtensions
    {
        /// <summary>
        /// Overloads RichTextBox.AppendText() with a version that takes a color as an argument.
        /// NOTE: color is "sticky", and will affect the next call to the built-in AppendText()
        /// method.
        /// </summary>
        /// <remarks>
        /// Adapted from https://stackoverflow.com/a/23402165/294248
        ///
        /// TODO(someday): figure out how to reset the color for future calls.
        /// </remarks>
        public static void AppendText(this RichTextBox box, string text, Color color)
        {

            TextRange tr = new TextRange(box.Document.ContentEnd, box.Document.ContentEnd);
            tr.Text = text;
            try
            {
                tr.ApplyPropertyValue(TextElement.ForegroundProperty,
                    new SolidColorBrush(color));
            }
            catch (FormatException ex)
            {
                Debug.WriteLine("RTB AppendText extension failed: " + ex);
            }
        }
    }

    /// <summary>
    /// BitmapSource extensions.
    /// </summary>
    public static class BitmapSourceExtensions
    {
        /// <summary>
        /// Creates a scaled copy of a BitmapSource.  Only scales up, using nearest-neighbor.
        /// </summary>
        public static BitmapSource CreateScaledCopy(this BitmapSource src, int scale)
        {
            // Simple approach always does a "blurry" scale.
            //return new TransformedBitmap(src, new ScaleTransform(scale, scale));

            // Adapted from https://weblogs.asp.net/bleroy/resizing-images-from-the-server-using-wpf-wic-instead-of-gdi
            // (found via https://stackoverflow.com/a/25570225/294248)
            BitmapScalingMode scalingMode = BitmapScalingMode.NearestNeighbor;

            int newWidth = (int)src.Width * scale;
            int newHeight = (int)src.Height * scale;

            var group = new DrawingGroup();
            RenderOptions.SetBitmapScalingMode(group, scalingMode);
            group.Children.Add(new ImageDrawing(src,
                    new Rect(0, 0, newWidth, newHeight)));
            var targetVisual = new DrawingVisual();
            var targetContext = targetVisual.RenderOpen();
            targetContext.DrawDrawing(group);
            var target = new RenderTargetBitmap(
                newWidth, newHeight, 96, 96, PixelFormats.Default);
            targetContext.Close();
            target.Render(targetVisual);
            var targetFrame = BitmapFrame.Create(target);
            return targetFrame;
        }
    }
}
