﻿using System;
using System.Collections.Generic;
using System.Text;
using System.Xml;
using System.Text.RegularExpressions;

namespace Cornerstone.ScraperEngine.Nodes {
    [ScraperNode("parse")]
    public class ParseNode: ScraperNode {
        #region Properties

        public string Input {
            get { return input; }
        } protected String input;

        public string Pattern {
            get { return pattern; }
        } protected String pattern;

        #endregion

        #region Methods

        public ParseNode(XmlNode xmlNode, bool debugMode)
            : base(xmlNode, debugMode) {

            // try to grab the input string
            try { input = xmlNode.Attributes["input"].Value; }
            catch (Exception) {
                logger.Error("Missing INPUT attribute on: " + xmlNode.OuterXml);
                loadSuccess = false;
                return;
            }

            // try to grab the regex pattern
            try { pattern = xmlNode.Attributes["regex"].Value; }
            catch (Exception) {
                logger.Error("Missing REGEX attribute on: " + xmlNode.OuterXml);
                loadSuccess = false;
                return;
            }    
        }

        public override void Execute(Dictionary<string, string> variables) {
            logger.Debug("executing parse: " + xmlNode.OuterXml);
            // parse variables from the input string
            string parsedInput = parseString(variables, input);
            string parsedName = parseString(variables, Name);

            // try to find matches via regex pattern
            Regex regEx = new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.Singleline);
            MatchCollection matches = regEx.Matches(parsedInput);

            if (matches.Count == 0) {
                logger.Debug("Parse node returned no results... " + xmlNode.OuterXml);
                return;
            }

            setVariable(variables, parsedName, matches[0].Value);

            // write matches and groups to variables
            int matchNum = 0;
            foreach (Match currMatch in matches) {
                // store the match itself
                string matchName = parsedName + "[" + matchNum + "]";
                setVariable(variables, matchName, currMatch.Value);

                // store the groups in the match
                for (int i = 1; i < currMatch.Groups.Count; i++)
                    setVariable(variables, matchName + "[" + (i - 1) + "]", currMatch.Groups[i].Value);

                matchNum++;
            }
        }

        #endregion
    }
}