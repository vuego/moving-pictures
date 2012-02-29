﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cornerstone.Database;
using Cornerstone.Database.Tables;
using System.Threading;
using MediaPortal.Plugins.MovingPictures.MainUI;

namespace MediaPortal.Plugins.MovingPictures.Database {
    internal class FilterHelperDBMovieInfo: DynamicFilterHelper<DBMovieInfo> {

        public override bool UpdateDynamicNode(DBNode<DBMovieInfo> node) {
            if (node.BasicFilteringField == DBField.GetFieldByDBName(typeof(DBMovieInfo), "year")) {
                UpdateYear(node);
                TranslateName(node);
                return true;
            }

            if (node.BasicFilteringField == DBField.GetFieldByDBName(typeof(DBMovieInfo), "date_added")) {
                UpdateDateAdded(node);
                TranslateName(node);
                return true;
            }
            
            if (node.BasicFilteringField == DBField.GetFieldByDBName(typeof(DBMovieInfo), "actors")) {
                UpdateActors(node);
                TranslateName(node);
                return true;
            }

            if (node.BasicFilteringField == DBField.GetFieldByDBName(typeof(DBMovieInfo), "title")) {
                UpdateAlphas(node);
                return true;
            }

            // for all other dynamic nodes, use generic processing but use a translated name
            node.UpdateDynamicNodeGeneric();
            TranslateName(node);
            return true;
        }

        // check if a translation exists for the given field, and if so, use that escape
        // sequence rather than the DB Field Name
        private void TranslateName(DBNode<DBMovieInfo> node) {
            string transString = "${" + node.BasicFilteringField.Name + "}";
            string result = Translation.ParseString(transString);
            if (result != node.BasicFilteringField.Name)
                node.Name = transString;
        }

        private void UpdateYear(DBNode<DBMovieInfo> node) {
            // grab list of possible years
            HashSet<string> allYears = node.DBManager.GetAllValues(node.BasicFilteringField, 
                                                                   node.BasicFilteringRelation, 
                                                                   node.GetFilteredItems());

            // build list of decades, each will coorespond to one subnode
            HashSet<int> decades = new HashSet<int>();
            foreach (string year in allYears) {
                int iYear;
                if (int.TryParse(year, out iYear))
                    decades.Add(iYear / 10);
            }

            // build lookup for subnodes and build list of nodes to remove
            List<DBNode<DBMovieInfo>> toRemove = new List<DBNode<DBMovieInfo>>();
            Dictionary<int, DBNode<DBMovieInfo>> nodeLookup = new Dictionary<int, DBNode<DBMovieInfo>>();
            foreach (DBNode<DBMovieInfo> currSubNode in node.Children) {
                if (!currSubNode.AutoGenerated)
                    continue;

                int decade = 0;
                if (int.TryParse(currSubNode.Filter.Criteria[0].Value.ToString(), out decade)) {
                    decade = (decade + 1) / 10;
                    if (decades.Contains(decade))
                        nodeLookup[decade] = currSubNode;
                }
                else {
                    toRemove.Add(currSubNode);
                }

            }

            // remove subnodes that are no longer valid
            foreach (DBNode<DBMovieInfo> currSubNode in toRemove) {
                node.Children.Remove(currSubNode);
                currSubNode.Delete();
            }

            // add subnodes that are missing
            foreach (int currDecade in decades) {
                if (nodeLookup.ContainsKey(currDecade))
                    continue;

                DBNode<DBMovieInfo> newSubNode = new DBNode<DBMovieInfo>();
                newSubNode.Name = (currDecade == 0) ? Translation.Unknown : Translation.GetByName("DecadeShort", currDecade);
                newSubNode.AutoGenerated = true;
                newSubNode.SortPosition = currDecade;

                DBFilter<DBMovieInfo> newFilter = new DBFilter<DBMovieInfo>();
                newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                newFilter.Name = currDecade.ToString();

                newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, ((currDecade * 10) - 1)));
                newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.LESS_THAN, ((currDecade * 10) + 10)));

                newSubNode.Filter = newFilter;

                // set the default sortorder for the gui
                DBMovieNodeSettings settings = new DBMovieNodeSettings();
                settings.UseDefaultSorting = false;
                settings.SortField = SortingFields.Year;

                newSubNode.AdditionalSettings = settings;


                node.Children.Add(newSubNode);
                newSubNode.Parent = node;
            }

            node.Children.Sort();
        }

        private void UpdateDateAdded(DBNode<DBMovieInfo> node) {

            // get all items that these nodes will apply to
            HashSet<DBMovieInfo> items = node.GetFilteredItems();

            // get all available periods
            string[] periods = new string[] {
                "WithinLastSevenDays",
                "WithinLastTwoWeeks",
                "WithinLastThirtyDays",
                "LastMonth",
                "TwoMonthsAgo",
                "ThreeMonthsAgo",
                "ThisYear",
                "LastYear",
                "Older"
            };

            // build lookup for subnodes and build list of nodes to remove
            List<DBNode<DBMovieInfo>> toRemove = new List<DBNode<DBMovieInfo>>();
            Dictionary<string, DBNode<DBMovieInfo>> nodeLookup = new Dictionary<string, DBNode<DBMovieInfo>>();
            foreach (DBNode<DBMovieInfo> currSubNode in node.Children) {
                if (!currSubNode.AutoGenerated)
                    continue;

                string period = currSubNode.Filter.Name;
                if (!periods.Contains(period))
                    toRemove.Add(currSubNode);
                else {
                    nodeLookup[period] = currSubNode;
                }
            }

            // remove subnodes that are no longer valid
            foreach (DBNode<DBMovieInfo> currSubNode in toRemove) {
                node.Children.Remove(currSubNode);
                currSubNode.Delete();
            }       

            // add subnodes that are missing
            int index = 0;
            foreach (string period in periods) {
                index++;

                if (nodeLookup.ContainsKey(period))
                    continue;

                DBFilter<DBMovieInfo> newFilter = new DBFilter<DBMovieInfo>();
                newFilter.Name = period;

                DBNode<DBMovieInfo> newSubNode = new DBNode<DBMovieInfo>();
                newSubNode.AutoGenerated = true;
                newSubNode.SortPosition = index;                
                
                switch(period) {
                    case "WithinLastSevenDays":
                        newSubNode.Name = Translation.GetByName("DatePartWithinDays", "7");
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ONE;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.EQUAL, "-7d"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-7d"));
                        break;
                    case "WithinLastTwoWeeks":
                        newSubNode.Name = Translation.GetByName("DatePartWithinDays", "14");
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ONE;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.EQUAL, "-14d"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-14d"));
                        break;
                    case "WithinLastThirtyDays":
                        newSubNode.Name = Translation.GetByName("DatePartWithinDays", "30");
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ONE;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.EQUAL, "-30d"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-30d"));
                        break;
                    case "LastMonth":
                        newSubNode.Name = Translation.LastMonth;
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.LESS_THAN, "M"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-1M"));
                        break;
                    case "TwoMonthsAgo":
                        newSubNode.Name = Translation.GetByName("DatePartAgo", 2, Translation.DateMonths);
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.LESS_THAN, "-1M"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-2M"));
                        break;
                    case "ThreeMonthsAgo":
                        newSubNode.Name = Translation.GetByName("DatePartAgo", 3, Translation.DateMonths);
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.LESS_THAN, "-2M"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-3M"));
                        break;
                    case "ThisYear":
                        newSubNode.Name = Translation.ThisYear;
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ONE;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.EQUAL, "Y"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "Y"));
                        break;
                    case "LastYear":
                        newSubNode.Name = Translation.LastYear;
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.LESS_THAN, "Y"));
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.GREATER_THAN, "-1Y"));
                        break;
                    case "Older":
                        newSubNode.Name = Translation.Older;
                        newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                        newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.LESS_THAN, "-1Y"));
                        break;
                }             
      
                
                newSubNode.Filter = newFilter;
                
                // set the default sortorder for the gui
                DBMovieNodeSettings settings = new DBMovieNodeSettings();
                settings.UseDefaultSorting = false;
                settings.SortField = SortingFields.DateAdded;
                settings.SortDirection = SortingDirections.Descending;

                newSubNode.AdditionalSettings = settings;
                
                node.Children.Add(newSubNode);
                newSubNode.Parent = node;
            }

            node.Children.Sort();
            
        }

        private void UpdateActors(DBNode<DBMovieInfo> node) {
            node.UpdateDynamicNodeGeneric();

            List<DBNode<DBMovieInfo>> toRemove = new List<DBNode<DBMovieInfo>>();
            foreach (DBNode<DBMovieInfo> currNode in node.Children) {
                if (currNode.DBManager == null)
                    currNode.DBManager = MovingPicturesCore.DatabaseManager;

                if (currNode.GetFilteredItems().Count < MovingPicturesCore.Settings.ActorLimit) {
                    toRemove.Add(currNode);
                }
            }

            foreach(DBNode<DBMovieInfo> currNode in toRemove) 
               node.Children.Remove(currNode);

        }

        private void UpdateAlphas(DBNode<DBMovieInfo> node) {
            var movies = node.GetFilteredItems();

            // store a list of nodes to remove
            var toRemove = node.Children.Where(n => n.AutoGenerated).ToList();

            // any movies starting with numeral for '#' entry
            if (movies.Any(m => Char.IsDigit(m.SortBy.TrimStart(), 0))) {
                if (!toRemove.Any(n => n.Name.Equals("#"))) {
                    // node not found, add it
                    DBNode<DBMovieInfo> newSubNode = new DBNode<DBMovieInfo>();
                    newSubNode.AutoGenerated = true;
                    newSubNode.Name = "#";
                    newSubNode.SortPosition = Convert.ToInt32('#');

                    DBFilter<DBMovieInfo> newFilter = new DBFilter<DBMovieInfo>();
                    newFilter.Name = "Alphas #";
                    newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ONE;
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "0"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "1"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "2"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "3"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "4"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "5"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "6"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "7"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "8"));
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, "9"));

                    newSubNode.Filter = newFilter;
                    newSubNode.AdditionalSettings = new DBMovieNodeSettings();
                    
                    node.Children.Add(newSubNode);
                    newSubNode.Parent = node;
                }
                else {
                    // we want to keep the existing node so don't remove it
                    toRemove.RemoveAll(n => n.Name.Equals("#"));
                }
            }
            
            // get list of unique starting letters from collection
            foreach (var alpha in movies.Where(m => !Char.IsDigit(m.SortBy, 0)).Select(m => m.SortBy.TrimStart().ToUpperInvariant()[0]).Distinct().OrderBy(m => m)) {
                string startLetter = alpha.ToString();

                if (!toRemove.Any(n => n.Name.Equals(startLetter))) {
                    // node not found, add it                    
                    DBNode<DBMovieInfo> newSubNode = new DBNode<DBMovieInfo>();
                    newSubNode.AutoGenerated = true;
                    newSubNode.Name = startLetter;
                    newSubNode.SortPosition = Convert.ToInt32(alpha);

                    DBFilter<DBMovieInfo> newFilter = new DBFilter<DBMovieInfo>();
                    newFilter.Name = "Alphas - " + startLetter;
                    newFilter.CriteriaGrouping = DBFilter<DBMovieInfo>.CriteriaGroupingEnum.ALL;
                    newFilter.Criteria.Add(createCriteria(node, DBCriteria<DBMovieInfo>.OperatorEnum.BEGINS_WITH, startLetter));
                 
                    newSubNode.Filter = newFilter;
                    newSubNode.AdditionalSettings = new DBMovieNodeSettings();

                    node.Children.Add(newSubNode);
                    newSubNode.Parent = node;
                }
                else {
                    // we want to keep the existing node so don't remove it
                    toRemove.RemoveAll(n => n.Name.Equals(startLetter));
                }
            }

            // remove any nodes left over with no items
            foreach (var currNode in toRemove) {
                node.Children.Remove(currNode);
            }

            node.Children.Sort();
        }

        private DBCriteria<DBMovieInfo> createCriteria(DBNode<DBMovieInfo> node, DBCriteria<DBMovieInfo>.OperatorEnum opEnum, object value) {
            DBCriteria<DBMovieInfo> criteria = new DBCriteria<DBMovieInfo>();
            criteria.Field = node.BasicFilteringField;
            criteria.Relation = node.BasicFilteringRelation;
            criteria.Operator = opEnum;
            criteria.Value = value;
            return criteria;
        }
    }
}