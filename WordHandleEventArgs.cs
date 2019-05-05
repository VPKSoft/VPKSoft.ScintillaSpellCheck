﻿#region License
/*
MIT License

Copyright(c) 2019 Petteri Kautonen

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
*/
#endregion

using System;

namespace VPKSoft.ScintillaSpellCheck
{
    /// <summary>
    /// Event arguments for the ignore word and add a word to dictionary events.
    /// Implements the <see cref="System.EventArgs" />
    /// </summary>
    /// <seealso cref="System.EventArgs" />
    public class WordHandleEventArgs: EventArgs
    {
        /// <summary>
        /// Gets or sets the word which is either requested to be ignored or to be requested to be added to the dictionary.
        /// </summary>
        /// <value>The word.</value>
        public string Word { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to add the word to the ignore list.
        /// </summary>
        public bool AddToIgnore { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether to add the word to the dictionary.
        /// </summary>
        public bool AddToDictionary { get; set; }
    }
}