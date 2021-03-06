﻿#region License
/*
MIT License

Copyright(c) 2020 Petteri Kautonen

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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows.Forms;
using ScintillaNET;
using VPKSoft.SpellCheck.ExternalDictionarySource;
using WeCantSpell.Hunspell;

namespace VPKSoft.ScintillaSpellCheck
{
    /// <summary>
    /// A class the spell check a <see cref="Scintilla"/> instance.
    /// Implements the <see cref="System.IDisposable" />
    /// </summary>
    /// <seealso cref="System.IDisposable" />
    public class ScintillaSpellCheck: IDisposable
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="ScintillaSpellCheck"/> class.
        /// </summary>
        /// <param name="scintilla">The scintilla to spell check for.</param>
        /// <param name="dictionaryFile">The dictionary file.</param>
        /// <param name="affixFile">The affix file.</param>
        public ScintillaSpellCheck(Scintilla scintilla, string dictionaryFile, string affixFile)
        {
            PreviousScintillaContextMenu = scintilla.ContextMenuStrip;

            this.scintilla = scintilla;

            this.scintilla.MouseDown += Scintilla_MouseDown;

            LoadDictionary(dictionaryFile, affixFile);

            // set the default indicator..
            SetIndicator();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScintillaSpellCheck"/> class.
        /// </summary>
        /// <param name="scintilla">The scintilla to spell check for.</param>
        public ScintillaSpellCheck(Scintilla scintilla)
        {
            PreviousScintillaContextMenu = scintilla.ContextMenuStrip;

            this.scintilla = scintilla;

            this.scintilla.MouseDown += Scintilla_MouseDown;

            // set the default indicator..
            SetIndicator();
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ScintillaSpellCheck"/> class.
        /// </summary>
        /// <param name="scintilla">The scintilla to spell check for.</param>
        /// <param name="dictionaryFile">The dictionary file.</param>
        /// <param name="affixFile">The affix file.</param>
        /// <param name="userDictionaryFile">The user's dictionary file.</param>
        /// <param name="userWordIgnoreListFile">The user's ignore word file.</param>
        public ScintillaSpellCheck(Scintilla scintilla, string dictionaryFile, string affixFile,
            string userDictionaryFile, string userWordIgnoreListFile)
        {
            PreviousScintillaContextMenu = scintilla.ContextMenuStrip;

            this.scintilla = scintilla;

            this.scintilla.MouseDown += Scintilla_MouseDown;

            LoadDictionary(dictionaryFile, affixFile);

            LoadUserDictionaryFromFile(userDictionaryFile);

            LoadUserWordIgnoreListFromFile(userWordIgnoreListFile);

            // set the default indicator..
            SetIndicator();
        }

        /// <summary>
        /// An user defined action to report an exception from the library.
        /// </summary>
        public static Action<Exception> ExceptionOccurredAction { get; set; }

        /// <summary>
        /// Handles the MouseDown event of the Scintilla control.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        private void Scintilla_MouseDown(object sender, MouseEventArgs e)
        {
            // the right button was clicked..
            if (e.Button == MouseButtons.Right)
            {
                var point = e.Location;

                // get the character index at the click location..
                int charPosition = scintilla.CharPositionFromPoint(point.X, point.Y);

                // validate the character index and the indicator style at the location..
                if (charPosition != -1 && scintilla.IndicatorOnFor(ScintillaIndicatorIndex, charPosition))
                {
                    // get the word start..
                    int start = scintilla.WordStartPosition(charPosition, OnlyWordCharacters);

                    // ..and end positions..
                    int end = scintilla.WordEndPosition(charPosition, OnlyWordCharacters);

                    // this is a deal breaker..
                    if (start == end)
                    {
                        // clean the previous menu (dispose)..
                        CleanPreviousSuggestMenu();
                        return;
                    }

                    // get the word at the mouse click location..
                    string word = scintilla.Text.Substring(start, end - start);

                    // initialize the word suggestion list..
                    List<string> suggestions = new List<string>();

                    // get the dictionary suggestions..
                    try
                    {
                        suggestions = DictionarySuggest(word).ToList();
                    }
                    catch (Exception ex)
                    {
                        ExceptionOccurredAction?.Invoke(ex);
                    }

                    if (UserDictionary != null)
                    {
                        try
                        {
                            var userSuggestions = UserDictionary.Suggest(word).ToList();
                            for (int i = 0; i < userSuggestions.Count; i++)
                            {
                                if (!suggestions.Exists(f => String.Compare(f, userSuggestions[i], StringComparison.Ordinal) == 0))
                                {
                                    suggestions.Add(userSuggestions[i]);
                                }
                            }

                            // sort the list as there are possible two word sets in the suggestion list..
                            suggestions.Sort((x, y) => string.Compare(x, y, StringComparison.InvariantCultureIgnoreCase));
                        }
                        catch (Exception ex)
                        {
                            ExceptionOccurredAction?.Invoke(ex);
                        }
                    }

                    // only create a menu if there are any suggestions..
                    if (suggestions.Count > 0 || 
                        ShowAddToDictionaryMenu || 
                        ShowIgnoreMenu)
                    {
                        // clean the previous menu (dispose)..
                        CleanPreviousSuggestMenu();

                        PreviousSuggestMenu = CreateSuggestionMenu(suggestions, point, (start, end), word);
                    }
                    else // don't leave the user without a menu..
                    {
                        // clean the previous menu (dispose)..
                        CleanPreviousSuggestMenu();                        
                    }
                }
                else
                {
                    // clean the previous menu (dispose)..
                    CleanPreviousSuggestMenu();
                }
            }
            else
            {
                // clean the previous menu (dispose)..
                CleanPreviousSuggestMenu();
            }
        }

        /// <summary>
        /// Cleans and disposes of the the previous suggest menu.
        /// </summary>
        private void CleanPreviousSuggestMenu()
        {
            // only do this if there are spell check suggestions in the collection..
            if (MenuItems.Count > 0)
            {
                // loop through the drop down items and unsubscribe the click events..
                foreach (ToolStripItem item in MenuItems)
                {
                    if (ToExistingMenu)
                    {
                        PreviousScintillaContextMenu?.Items.RemoveByKey(item.Name);
                    }
                    item.Click -= OnSuggestMenuClick;
                }

                // clear the items after the event un-subscription..
                PreviousSuggestMenu?.Items.Clear();

                // dispose of the previous suggest menu..
                PreviousSuggestMenu?.Dispose();

                // assign a null value for comparison within the class..
                PreviousSuggestMenu = null;

                // clear the list of menu items..
                MenuItems.Clear();
            }

            // set the Scintilla's previous context menu back in place..
            scintilla.ContextMenuStrip = PreviousScintillaContextMenu;
        }

        /// <summary>
        /// Gets or sets the previous suggest menu.
        /// </summary>
        private ContextMenuStrip PreviousSuggestMenu { get; set; }

        /// <summary>
        /// Gets or sets the previous context menu of the <see cref="Scintilla"/>.
        /// </summary>
        private ContextMenuStrip PreviousScintillaContextMenu { get; set; }

        /// <summary>
        /// Gets or set a value indicating whether the menu should be appended to an existing context menu.
        /// </summary>
        public bool ToExistingMenu { get; set; } = true;

        /// <summary>
        /// An internal list of currently created spell check suggestion menu items.
        /// </summary>
        private List<ToolStripItem> MenuItems { get; set; } = new List<ToolStripItem>();

        /// <summary>
        /// A list of ignored words.
        /// </summary>
        private static List<string> IgnoreList { get; set; } = new List<string>();

        /// <summary>
        /// Gets or sets a value indicating whether to show the ignore word menu item.
        /// </summary>
        public bool ShowIgnoreMenu { get; set; } = false;

        /// <summary>
        /// Gets or set the text used in the ignore word menu item.
        /// </summary>
        public string MenuIgnoreText { get; set; } = "Ignore word \"{0}\".";

        /// <summary>
        /// Gets or sets a value indicating whether to show the add a word to the dictionary menu item.
        /// </summary>
        public bool ShowAddToDictionaryMenu { get; set; } = false;

        /// <summary>
        /// Gets or sets the text used in the add word to dictionary menu item.
        /// </summary>
        public string MenuAddToDictionaryText { get; set; } = "Add word \"{0}\" to the dictionary.";

        /// <summary>
        /// Gets or sets a value indicating whether to show a disabled menu item on top of the other dictionary menu item.
        /// </summary>
        public bool ShowDictionaryTopMenuItem { get; set; } = false;

        /// <summary>
        /// Gets or set a text used in the spell checking "header" menu if one is enabled.
        /// </summary>
        public string MenuDictionaryTopItemText { get; set; } = "Spell checking";

        /// <summary>
        /// Gets or sets a value indicating whether to add a bottom separator menu item to the dictionary.
        /// </summary>
        public bool AddBottomSeparator { get; set; } = true;


        /// <summary>
        /// Creates a new suggestion menu for words.
        /// </summary>
        /// <param name="suggestions">A collection of suggestions to show to the user.</param>
        /// <param name="point">The point where the menu should be shown at.</param>
        /// <param name="pos">The position of the word for suggest alternatives to.</param>
        /// <param name="word">The word from which <paramref name="suggestions"/> where gotten from.</param>
        /// <returns>A <see cref="ContextMenuStrip"/> containing suggestions for the word.</returns>
        private ContextMenuStrip CreateSuggestionMenu(List<string> suggestions, Point point, (int start, int end) pos, string word)
        {
            // clean the previous menu (dispose)..
            CleanPreviousSuggestMenu();

            // create a new ContextMenuStrip class instance..
            ContextMenuStrip suggestMenu = new ContextMenuStrip();

            // if there are no correction suggestions then just return with an empty menu..
            if (suggestions.Count == 0 && 
                !ShowAddToDictionaryMenu && 
                !ShowIgnoreMenu)
            {
                return suggestMenu;
            }

            int nameCounter = 0;

            // if a title menu is wanted to be shown, then create a one..
            if (ShowDictionaryTopMenuItem)
            {
                MenuItems.Add(new ToolStripMenuItem(string.Format(MenuDictionaryTopItemText, word))
                {
                    Name = "VPKSoft.ScintillaSpellCheck_title",
                    Enabled = false,
                });

                // add a separator with the 
                MenuItems.Add(new ToolStripSeparator {Name = "VPKSoft.ScintillaSpellCheck" + nameCounter++});
            }

            // loop through the suggestions..
            foreach (var suggestion in suggestions)
            {
                // ..and create ToolStripMenuItem's out of them..                
                MenuItems.Add(new ToolStripMenuItem(suggestion, null, OnSuggestMenuClick)
                {
                    Tag = pos, Name = "VPKSoft.ScintillaSpellCheck" + nameCounter++
                }); // save the position to the tag..
            }

            // add a separator if additional menus are requested..
            if ((ShowIgnoreMenu || ShowAddToDictionaryMenu) && suggestions.Count > 0)
            {
                MenuItems.Add(new ToolStripSeparator {Name = "VPKSoft.ScintillaSpellCheck" + nameCounter++});
            }

            if (ShowIgnoreMenu)
            {
                // Add the ignore word menu if the ShowIgnoreMenu flag is set..
                MenuItems.Add(new ToolStripMenuItem(string.Format(MenuIgnoreText, word), null, OnSuggestMenuClick)
                {
                    Tag = word, Name = "VPKSoft.ScintillaSpellCheck_ignore"
                }); // save the position to the tag..
            }

            if (ShowAddToDictionaryMenu)
            {
                // Add the add a word to dictionary ignore menu if the ShowAddToDictionaryMenu flag is set..
                MenuItems.Add(new ToolStripMenuItem(string.Format(MenuAddToDictionaryText, word), null, OnSuggestMenuClick)
                {
                    Tag = word, Name = "VPKSoft.ScintillaSpellCheck_add"
                }); // save the position to the tag..
            }

            // if a bottom separator menu was requested to be added..
            if (AddBottomSeparator)
            {
                // ..then just add it..
                MenuItems.Add(new ToolStripSeparator {Name = "VPKSoft.ScintillaSpellCheck" + nameCounter});
            }

            // if not adding to an existing menu add the items to the previously
            // created suggestMenu..
            if (!ToExistingMenu)
            {
                foreach (var menuItem in MenuItems)
                {
                    suggestMenu.Items.Add(menuItem);
                }
            }

            // save the previous context menu of the Scintilla control so it can be restored..
            PreviousScintillaContextMenu = scintilla.ContextMenuStrip;

            if (ToExistingMenu && PreviousScintillaContextMenu != null)
            {
                for (int i = 0; i < MenuItems.Count; i++)
                {
                    PreviousScintillaContextMenu.Items.Insert(i, MenuItems[i]);
                }
            }
            else if (!ToExistingMenu)
            {
                // set the just created context menu as a new context menu for the Scintilla control..
                scintilla.ContextMenuStrip = suggestMenu;               
            }

            // display the menu immediately at the given location..
            if (!ToExistingMenu)
            {
                suggestMenu.Show(scintilla, point);
            }

            // return the created context menu..
            return suggestMenu;
        }

        /// <summary>
        /// A delegate for the <see cref="ScintillaSpellCheck.WordIgnoreRequested"/> and <see cref="ScintillaSpellCheck.WordAddDictionaryRequested"/> events.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The <see cref="WordHandleEventArgs"/> instance containing the event data.</param>
        public delegate void OnWordHandleRequest(object sender, WordHandleEventArgs e);

        /// <summary>
        /// An event which is fired when a user requests a word to be added to an ignore list.
        /// </summary>
        public event OnWordHandleRequest WordIgnoreRequested;

        /// <summary>
        /// An event which is fired when a user requests a word to be added to the dictionary.
        /// </summary>
        public event OnWordHandleRequest WordAddDictionaryRequested;

        /// <summary>
        /// An event which is fired when a user corrects a word using the context menu.
        /// </summary>
        public event OnWordHandleRequest UserWordReplace;

        /// <summary>
        /// An event which is fired when a user has corrected a word using the context menu (the <see cref="Scintilla"/> text changed event has fired).
        /// </summary>
        public event OnWordHandleRequest AfterUserWordReplace;


        /// <summary>
        /// Handles the <see cref="E:SuggestMenuClick" /> event.
        /// </summary>
        /// <param name="sender">The sender of the event.</param>
        /// <param name="e">The <see cref="EventArgs"/> instance containing the event data.</param>
        private void OnSuggestMenuClick(object sender, EventArgs e)        
        {            
            // get the clicked menu item from the suggest a word context menu..
            var clickedItem = (ToolStripMenuItem) sender;

            // a menu with a request to ignore a word was clicked..
            if (clickedItem.Name == "VPKSoft.ScintillaSpellCheck_ignore")
            {
                // raise an event if subscribed..
                WordIgnoreRequested?.Invoke(sender,
                    new WordHandleEventArgs
                    {
                        Word = clickedItem.Tag.ToString(), AddToDictionary = false, AddToIgnore = true,
                        IsWordReplace = false, ScintillaSpellCheck = this,
                    });
            }
            // a menu with a request to add a word to the dictionary was clicked..
            else if (clickedItem.Name == "VPKSoft.ScintillaSpellCheck_add")
            {
                // raise an event if subscribed..
                WordAddDictionaryRequested?.Invoke(sender,
                    new WordHandleEventArgs
                    {
                        Word = clickedItem.Tag.ToString(), AddToDictionary = true, AddToIgnore = false,
                        IsWordReplace = false, ScintillaSpellCheck = this,
                    });
            }
            // the "normal" case..
            else 
            {
                // get the word position that the suggestion menu replaces..
                var wordPos = ((int start, int end)) clickedItem.Tag;

                // select the miss-spelled word..
                scintilla.SelectionStart = wordPos.start;
                scintilla.SelectionEnd = wordPos.end;

                // get the word for the event..
                var wordFrom = scintilla.Text.Substring(wordPos.start, wordPos.end - wordPos.start);

                // raise an event if subscribed..
                UserWordReplace?.Invoke(sender,
                    new WordHandleEventArgs
                    {
                        AddToDictionary = false, AddToIgnore = false, IsWordReplace = true, WordFrom = wordFrom,
                        WordTo = clickedItem.Text, ScintillaSpellCheck = this,
                    });

                // replace the miss-spelled word with user "input"..
                scintilla.ReplaceSelection(clickedItem.Text);

                // raise an event if subscribed..
                AfterUserWordReplace?.Invoke(sender,
                    new WordHandleEventArgs
                    {
                        AddToDictionary = false, AddToIgnore = false, IsWordReplace = true, WordFrom = wordFrom,
                        WordTo = clickedItem.Text, ScintillaSpellCheck = this,
                    });
            }

            // clean the previous menu (dispose)..
            CleanPreviousSuggestMenu();
        }

        /// <summary>
        /// Gets or sets the dictionary that the <see cref="WeCantSpell.Hunspell"/> class library has created.
        /// </summary>
        public WordList Dictionary { get; set; }

        /// <summary>
        /// Gets or sets the optional <see cref="IExternalDictionarySource"/> external dictionary interface to replace the Hunspell dictionary.
        /// </summary>
        public static IExternalDictionarySource ExternalDictionary { get; set; }

        /// <summary>
        /// Gets word suggestions from either the <see cref="ExternalDictionary"/> or the given Hunspell dictionary set via the <see cref="LoadDictionary"/> method.
        /// </summary>
        /// <param name="word">The word.</param>
        /// <returns>A IEnumerable&lt;System.String&gt; containing the suggestions.</returns>
        private IEnumerable<string> DictionarySuggest(string word)
        {
            return Dictionary == null ? ExternalDictionary?.Suggest(word) : Dictionary.Suggest(word);
        }

        /// <summary>
        /// Checks if the word is spelled correctly using either the <see cref="ExternalDictionary"/> or the given Hunspell dictionary set via the <see cref="LoadDictionary"/> method.
        /// </summary>
        /// <param name="word">The word to check for.</param>
        /// <returns><c>true</c> if the word is spelled correctly, <c>false</c> otherwise.</returns>
        private bool DictionaryCheck(string word)
        {
            return (Dictionary?.Check(word) ?? ExternalDictionary?.Check(word)) == true;
        }

        /// <summary>
        /// Gets or sets the user dictionary that the <see cref="WeCantSpell.Hunspell"/> class library has created.
        /// </summary>
        private WordList UserDictionary { get; set; }

        /// <summary>
        /// Gets or sets the list containing the user dictionary words.
        /// </summary>
        private static List<string> UserDictionaryWords { get; set; } = new List<string>();

        /// <summary>
        /// Loads the dictionary in to the <see cref="Dictionary"/> property.
        /// </summary>
        /// <param name="dictionaryFile">The dictionary file (*.dic).</param>
        /// <param name="affixFile">The affix file (*.aff).</param>
        public void LoadDictionary(string dictionaryFile, string affixFile)
        {
            // read the data from the streams and dispose of them..
            using (var dictionaryStream = File.OpenRead(dictionaryFile))
            {
                using (var affixStream = File.OpenRead(affixFile))
                {
                    // create a new dictionary..
                    Dictionary = WordList.CreateFromStreams(dictionaryStream, affixStream);
                }
            }
        }

        /// <summary>
        /// Creates a user dictionary.
        /// </summary>
        /// <param name="wordStrings">A list of words to use within a user dictionary.</param>
        public void LoadUserDictionary(params string[] wordStrings)
        {
            // create the user dictionary..
            UserDictionary = WordList.CreateFromWords(wordStrings);

            // save the words to the list, so the user dictionary can be
            // reconstructed..
            UserDictionaryWords = new List<string>(wordStrings);
        }

        /// <summary>
        /// Gets a word array from a file.
        /// </summary>
        /// <param name="fileName">The file name to get the array from.</param>
        /// <returns></returns>
        private string[] CreateWordListFromFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                // return an empty array for a non-existent file..
                return new string[0];
            }

            // read all the lines from the given file..
            var lines = File.ReadAllLines(fileName);

            // join the lines with separator character of ' '..
            string wordString = string.Join(" ", lines);

            return wordString.Split(' ');
        }

        /// <summary>
        /// Creates a user ignore word list from file. The file is divided with lines and the words are extracted via string.Split(' ') method.
        /// </summary>
        /// <param name="fileName">The file name to load the user word ignore list from.</param>
        public void LoadUserWordIgnoreListFromFile(string fileName)
        {
            if (!File.Exists(fileName))
            {
                return;
            }

            // get the ignored words..
            IgnoreList = new List<string>(CreateWordListFromFile(fileName));
        }

        /// <summary>
        /// Saves or appends to the ignore word list to a given file.
        /// </summary>
        /// <param name="fileName">The name of the file to save or to append to.</param>
        public void SaveUserWordIgnoreListToFile(string fileName)
        {
            List<string> ignoreList = new List<string>(CreateWordListFromFile(fileName));

            ignoreList.AddRange(IgnoreList);

            ignoreList = ignoreList.Distinct().ToList();

            File.WriteAllLines(fileName, ignoreList);
        }

        /// <summary>
        /// Saves or appends to the user dictionary to a given file.
        /// </summary>
        /// <param name="fileName">The name of the file to save or to append to.</param>
        public void SaveUserDictionaryToFile(string fileName)
        {
            List<string> userDictionaryList = new List<string>(CreateWordListFromFile(fileName));

            userDictionaryList.AddRange(UserDictionaryWords);

            userDictionaryList = userDictionaryList.Distinct().ToList();

            File.WriteAllLines(fileName, userDictionaryList);
        }

        /// <summary>
        /// Creates a user dictionary from file. The file is divided with lines and the words are extracted via string.Split(' ') method.
        /// </summary>
        /// <param name="fileName">The file name to load the user dictionary from.</param>
        public void LoadUserDictionaryFromFile(string fileName)
        {
            // create the user dictionary..
            LoadUserDictionary(CreateWordListFromFile(fileName));
        }

        /// <summary>
        /// Add a word to the user dictionary.
        /// </summary>
        /// <param name="word">A word to add to the user dictionary.</param>
        /// <returns>True if the word was successfully added to the user dictionary; otherwise false.</returns>
        public bool AddToUserDictionary(string word)
        {
            if (UserDictionaryWords.Exists(f =>
                string.Compare(f, word, StringComparison.Ordinal) == 0))
            {
                return false;
            }

            UserDictionaryWords.Add(word);
            LoadUserDictionary(UserDictionaryWords.ToArray());
            return true;
        }

        /// <summary>
        /// Add a word to the user ignore word list.
        /// </summary>
        /// <param name="word">A word to add to the user ignore word list.</param>
        /// <returns>True if the word was successfully added to the user ignore word list; otherwise false.</returns>
        public bool AddToUserIgnoreList(string word)
        {
            if (IgnoreList.Exists(f =>
                string.Compare(f, word, StringComparison.Ordinal) == 0))
            {
                return false;
            }

            IgnoreList.Add(word);
            
            return true;
        }

        /// <summary>
        /// Gets the words in the user dictionary.
        /// </summary>
        /// <returns>The words listed in the user dictionary.</returns>
        public List<string> GetUserDictionaryWords()
        {
            return UserDictionaryWords;
        }

        /// <summary>
        /// Gets the words in the user ignore word list.
        /// </summary>
        /// <returns>The words listed in the user ignore word list.</returns>
        public List<string> GetUserIgnoreWords()
        {
            return IgnoreList;
        }

        /// <summary>
        /// Sets the <see cref="Scintilla"/> indicator for underlining the miss-spelled words.
        /// </summary>
        private void SetIndicator()
        {
            // if the current indicator is different from the class indicator property index..
            if (scintilla.IndicatorCurrent != ScintillaIndicatorIndex)
            {
                // ..save the current indicator for to be able to set it back to it's previous value..
                ScintillaIndicatorCurrent = scintilla.IndicatorCurrent;
            }

            // set the styles from the public properties--
            scintilla.Indicators[ScintillaIndicatorIndex].Style = ScintillaIndicatorStyle;
            scintilla.Indicators[ScintillaIndicatorIndex].ForeColor = ScintillaIndicatorColor;

            // set the current indicator value (0-31)..
            scintilla.IndicatorCurrent = ScintillaIndicatorIndex;
        }

        /// <summary>
        /// A field where the Scintilla instance is saved from the constructor.
        /// </summary>
        private readonly Scintilla scintilla;

        /// <summary>
        /// A field indicating whether the <see cref="Next"/> method call has reached the documents ending.
        /// </summary>
        private bool endReached;

        /// <summary>
        /// The scintilla indicator index field for the <see cref="ScintillaIndicatorIndex"/> property.
        /// </summary>
        private int scintillaIndicatorIndex = 31; // set to the maximum to not to override the other indicators hopefully..

        /// <summary>
        /// Gets or sets the index of the <see cref="Scintilla"/> indicator. The value must be within range of 0 to 31.
        /// </summary>
        /// <value>The index of the scintilla indicator.</value>
        public int ScintillaIndicatorIndex
        {
            get => scintillaIndicatorIndex; // just return the value..

            set
            {
                // save the property value..
                scintillaIndicatorIndex = value;

                // reset the Scintilla indicator for underlining the miss-spelled words..
                ClearSpellCheck();
            }
        }

        /// <summary>
        /// Gets or sets the previous word start with the spell checking.
        /// </summary>
        private int PreviousWordStart { get; set; } = -1;

        /// <summary>
        /// Gets or sets the next position to continue the spell checking from.
        /// </summary>
        private int NextPosition { get; set; }

        /// <summary>
        /// Gets or sets the current <see cref="Scintilla"/> indicator index for restoring it after spell checking has been done.
        /// </summary>
        private int ScintillaIndicatorCurrent { get; set; }

        /// <summary>
        /// A field to hold the color for the indicator for a miss-spelled word in the <see cref="Scintilla"/>.
        /// </summary>
        private Color scintillaIndicatorColor = Color.Red;

        /// <summary>
        /// Gets or sets the color for the indicator for a miss-spelled word in the <see cref="Scintilla"/>.
        /// </summary>
        public Color ScintillaIndicatorColor 
        {
            get => scintillaIndicatorColor; // just return the value..

            set
            {
                // set the value..
                scintillaIndicatorColor = value;

                // reset the Scintilla indicator for underlining the miss-spelled words..
                ClearSpellCheck();
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether to to stop searching at the first non-word character regardless of whether the search started at a word or non-word character or
        /// to use the first character in the search as a word or non-word indicator and then search for that word or non-word boundary.
        /// </summary>
        // Comment (C): ScintillaNET..
        public bool OnlyWordCharacters { get; set; } = true;

        /// <summary>
        /// Clears the spell check indicator from the <see cref="Scintilla"/> control.
        /// </summary>
        public void ClearSpellCheck()
        {
            SetIndicator();
            scintilla.IndicatorClearRange(0, scintilla.TextLength);
            scintilla.IndicatorCurrent = ScintillaIndicatorCurrent;
        }

        /// <summary>
        /// A field holding the <see cref="ScintillaIndicatorStyle"/> property value.
        /// </summary>
        private IndicatorStyle scintillaIndicatorStyle = IndicatorStyle.Squiggle;

        /// <summary>
        /// Gets or sets the <see cref="Scintilla"/> indicator style which is the visual appearance of an indicator in the <see cref="Scintilla"/> control.
        /// </summary>
        public IndicatorStyle ScintillaIndicatorStyle 
        { 
            get => scintillaIndicatorStyle; // just return the value..

            set
            {
                // set the value..
                scintillaIndicatorStyle = value;

                // reset the Scintilla indicator for underlining the miss-spelled words..
                ClearSpellCheck();
            }
        }

        /// <summary>
        /// Resets the spell checking position of this instance.
        /// </summary>
        public void Reset()
        {
            // set the previous word start to the class instance creation default..
            PreviousWordStart = -1; 

            // set the next word position to the class instance creation default..
            NextPosition = 0;

            // re-set the indicator for underlining the miss-spelled words..
            SetIndicator();
        }

        /// <summary>
        /// Gets or sets the word boundary regex used with the <see cref="SpellCheckScintillaFast"/> method.
        /// </summary>
        public Regex WordBoundaryRegex { get; set; } = new Regex( "\\b\\w+\\b", RegexOptions.Compiled);

        /// <summary>
        /// Marks miss-spelled words of the <see cref="Scintilla"/> control using compiled regular expression to match the words.
        /// </summary>
        public void SpellCheckScintillaFast()
        {
            // first re-set the search..
            Reset();

            // clear the previous spell check markings..
            scintilla.IndicatorClearRange(0, scintilla.TextLength);

            var words = WordBoundaryRegex.Matches(scintilla.Text);

            // initialize a list of failed words to speed up the spell checking..
            List<string> failedWords = new List<string>();

            // initialize a list of passed words to speed up the spell checking..
            List<string> wordOkList = new List<string>();


            for (int i = 0; i < words.Count; i++)
            {
                if (wordOkList.Contains(words[i].Value) /* .Exists(f => f.Equals(words[i].Value, StringComparison.InvariantCultureIgnoreCase))*/)
                {
                    // already passed the check..
                    continue;
                }

                // check if the word is already in the list of failed words..
                bool inFailedWordList = failedWords.Contains(words[i].Value); // .Exists(f => f.Equals(words[i].Value, StringComparison.InvariantCultureIgnoreCase));

                if (inFailedWordList)
                {
                    // ..mark it with an indicator..
                    scintilla.IndicatorFillRange(words[i].Index, words[i].Length);
                    continue;
                }

                // check the ignore list..
                if (IgnoreList.Exists(f =>
                        string.Equals(f, words[i].Value, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // flag the word as ok to prevent re-checking the validity..
                    wordOkList.Add(words[i].Value);

                    // the word is valid via user dictionary or user ignore list..
                    continue;
                }

                // validate the possible user dictionary..
                bool userDictionaryOk = UserDictionary?.Check(words[i].Value) ?? false;

                if (userDictionaryOk)
                {
                    // flag the word as ok to prevent re-checking the validity..
                    wordOkList.Add(words[i].Value);

                    // the word is valid via user dictionary or user ignore list..
                    continue;
                }

                // validate the word with the loaded dictionary..
                if (!DictionaryCheck(words[i].Value))
                {
                    // flag the word as invalid to prevent re-checking the validity..
                    failedWords.Add(words[i].Value);

                    // ..mark it with an indicator..
                    scintilla.IndicatorFillRange(words[i].Index, words[i].Length);
                }
                else
                {
                    // flag the word as ok to prevent re-checking the validity..
                    wordOkList.Add(words[i].Value);
                }
            }

            // re-set the saved indicator value to the Scintilla instance..
            scintilla.IndicatorCurrent = ScintillaIndicatorCurrent;
        }

        /// <summary>
        /// Marks miss-spelled words of the <see cref="Scintilla"/> control.
        /// </summary>
        public void SpellCheckScintilla()
        {
            // first re-set the search..
            Reset();

            // clear the previous spell check markings..
            scintilla.IndicatorClearRange(0, scintilla.TextLength);


            // create a variable for the next word..
            (int start, int end, int length, string word) word;

            // loop while a word which spelling to is found..
            while ((word = Next()) != default)
            {
                // if the WeCantSpell.Hunspell disagrees with the words spelling..
                if (!DictionaryCheck(word.word) && !IgnoreList.Exists(f =>
                        string.Equals(f, word.word, StringComparison.InvariantCultureIgnoreCase)))
                {
                    // ..mark it with an indicator..
                    scintilla.IndicatorFillRange(word.start, word.end - word.start);
                }
            }

            // re-set the saved indicator value to the Scintilla instance..
            scintilla.IndicatorCurrent = ScintillaIndicatorCurrent;
        }

        /// <summary>
        /// Gets a next word in the <see cref="Scintilla"/> class instance to check for miss-spelling.
        /// </summary>
        /// <returns>(System.Int32 start, System.Int32 end, System.Int32 length, System.String word).</returns>
        (int start, int end, int length, string word) Next()
        {
            // if the end has already been reached within the Scintilla document..
            if (endReached)
            {
                // ..just return with a default(T)..
                return default;
            }

            // loop through the words within the Scintilla instance..
            for (int i = NextPosition; i < scintilla.TextLength; i++)
            {
                // get the word starting position at a given position..
                int start = scintilla.WordStartPosition(i, OnlyWordCharacters);

                // if the found word index differs from the previous occurrence..
                if (start != PreviousWordStart)
                {
                    // save the value to the previous occurrence value..
                    PreviousWordStart = start;

                    // get the ending position of the word..
                    int end = scintilla.WordEndPosition(i, OnlyWordCharacters);

                    // some validation in case a recursion is required..
                    if ((end == 0 && PreviousWordStart == -1) || start == end)
                    {
                        NextPosition++; // ..we must advance..
                        return Next(); // call your self..
                    }

                    // set the next position to the found word's ending position..
                    NextPosition = end; 

                    // return the found word and its position and length..
                    return (start, end, end - start, scintilla.Text.Substring(start, end - start));
                }
            }

            // if nothing was returned withing the loop, assume that the end has been reached..
            endReached = true;

            // return the default(T)..
            return default;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            scintilla.MouseDown -= Scintilla_MouseDown;

            // clean the previous menu (dispose)..
            CleanPreviousSuggestMenu();
        }
    }
}
