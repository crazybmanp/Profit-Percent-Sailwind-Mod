using HarmonyLib;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

using Object = UnityEngine.Object;

namespace ProfitPercent
{
    /// <summary>
    /// Patches for the Profit Percent mod
    /// </summary>
    public class ProfitPercentPatches
    {
        #region TextMeshes
        //New columns
        private static TextMesh productionText;
        private static TextMesh perPoundText;

        //Vanilla columns
        private static TextMesh islandNames;
        private static TextMesh buyColumn;
        private static TextMesh goodName;
        private static TextMesh sellColumn;
        private static TextMesh profitColumn;
        private static TextMesh daysAgo;
        private static TextMesh percentText;
        private static TextMesh conversionFees;
        private static Transform highlightBar;

        //Best deals
        private static TextMesh bdBestDeals = new TextMesh();
        private static TextMesh bdPercent = new TextMesh();
        private static TextMesh bdPerPound = new TextMesh();
        private static TextMesh bdAbsolute = new TextMesh();
        #endregion
        private static MethodInfo buypInfo;
        private static MethodInfo sellpInfo;
        private static Good[] goods;
        
        public static Dictionary<int, float[]> portProd = new Dictionary<int, float[]>();   //production values dictionary

        private const float charSize = 0.8f;
        private const float spacing = 0.073f;    //spacing for the highlight bar
        private static float[] goodWeights;
        private static string[] goodNames;
        private static float[] colX = new float[7]; //computed column X positions [0]=daysAgo [1]=P. [2]=buy [3]=sell [4]=profit [5]=% [6]=p.pound

        //PATCHES
        public static void AwakePatch(EconomyUI __instance)
        {   //initialises the new UI at game start
            
            //Store MethodInfos
            buypInfo = AccessTools.Method(typeof(EconomyUI), "GetBuyPrice");
            sellpInfo = AccessTools.Method(typeof(EconomyUI), "GetSellPrice");

            Transform detailsUI = __instance.transform.Find("good details (right panel)").Find("details UI");

            //Assign vanilla columns references
            GetVanillaColumns(__instance, detailsUI);
            AddModColumns(__instance, detailsUI);
            DisableUnusedUI(detailsUI);

            //Cache port production values
            InitializeProd();
        }
        public static void MainPatch(int[][] ___bookmarkIslands, int ___currentBookmark, IslandMarket ___currentIsland)
        {   //Main patch for the trade UI - This is called every time the page is refreshed

            //Capitalise the good name
            if (goodName != null) goodName.text = Capitalize(goodName.text);

            if (productionText != null) productionText.text = "";
            if (perPoundText != null) perPoundText.text = "";
            
            if (goods == null) InitializeGoods();

            if (ProfitPercentMain.showBestDealsConfig.Value && bdBestDeals != null)
            {
                bdBestDeals.text = "<color=#4D0000>★ Best Deals! ★</color>";
                FindBestDeals(___bookmarkIslands, ___currentBookmark, ___currentIsland);
            }
            else if (bdBestDeals != null)
            {
                bdBestDeals.text = "";
                bdPercent.text = "";
                bdPerPound.text = "";
                bdAbsolute.text = "";
            }

            if (profitColumn == null || percentText == null) return;

            string[] nativeProfits = profitColumn.text.Split('\n');
            string[] nativePercents = percentText.text.Split('\n');
            
            string newProfit = "";
            string newPercent = "";

            int goodIndex = EconomyUI.instance.currentSelectedGood;

            for (int i = 0; i < ___bookmarkIslands[___currentBookmark].Length; i++)
            {
                int portIndex = ___bookmarkIslands[___currentBookmark][i];
                
                if (portIndex == ___currentIsland.GetPortIndex())
                {
                    SetHighlightBar(i);
                }

                GetProduction(portIndex, goodIndex);

                string nProf = i < nativeProfits.Length ? nativeProfits[i] : "";
                string nPerc = i < nativePercents.Length ? nativePercents[i] : "";
                
                if (string.IsNullOrEmpty(nProf) && string.IsNullOrEmpty(nPerc)) continue; // skip trailing empty lines

                if (portIndex < 0 || portIndex >= ___currentIsland.knownPrices.Length || ___currentIsland.knownPrices[portIndex] == null || ___currentIsland.knownPrices[portIndex].buyPrices == null)
                {
                    perPoundText.text += "?\n";
                    newProfit += nProf.Replace("|", "").Trim() + "\n";
                    newPercent += nPerc.Replace("|", "").Trim() + "\n";
                }
                else
                {
                    int buyp = BuyP(___currentIsland.GetPortIndex(), goodIndex);
                    int sellp = SellP(portIndex, goodIndex);
                    int profit = sellp - buyp;

                    float profitPercent = buyp != 0 ? Mathf.Round(((float)profit / buyp) * 100f) : float.PositiveInfinity;
                    float cargoWeight = goodWeights[goodIndex];
                    float profitPerPound = (float)Math.Round(profit / cargoWeight, 2);

                    string cleanProf = nProf.Replace("|", "").Trim();
                    string cleanPerc = nPerc.Replace("|", "").Trim();

                    if (ProfitPercentMain.coloredTextConfig.Value)
                    {
                        int higherThreshold = Math.Max(ProfitPercentMain.blueThresholdConfig.Value, ProfitPercentMain.greenThresholdConfig.Value);
                        int lowerThreshold = Math.Min(ProfitPercentMain.blueThresholdConfig.Value, ProfitPercentMain.greenThresholdConfig.Value);

                        string hexColor = "";
                        if (float.IsInfinity(profitPercent)) hexColor = "#CC7F00";
                        else if (profitPercent > higherThreshold) hexColor = "#051139";
                        else if (profitPercent > lowerThreshold) hexColor = "#003300";
                        else if (profitPercent < 0f) hexColor = "#4D0000";
                        else hexColor = "#CC7F00";

                        newProfit += $"<color={hexColor}>{cleanProf}</color>\n";
                        newPercent += $"<color={hexColor}>{cleanPerc}</color>\n";
                        perPoundText.text += $"<color={hexColor}>{(float.IsInfinity(profitPercent) ? "- " : profitPerPound.ToString())}</color>\n";
                    }
                    else
                    {
                        newProfit += cleanProf + "\n";
                        newPercent += cleanPerc + "\n";
                        perPoundText.text += (float.IsInfinity(profitPercent) ? "- \n" : profitPerPound.ToString() + "\n");
                    }
                }
            }

            profitColumn.text = newProfit;
            percentText.text = newPercent;
            if (buyColumn != null) buyColumn.text = buyColumn.text.Replace("|", "").Replace(" ", "");
            if (sellColumn != null) sellColumn.text = sellColumn.text.Replace("|", "").Replace(" ", "");
        }
        public static void ButtonPatch(EconomyUIButton __instance)
        {   //Automatically get the receipt when closing the trade UI
            
            if (__instance.name == "bookmark_button_X" && ProfitPercentMain.autoReceiptConfig.Value && EconomyUIReceiptScribe.instance.ReceiptAvailable())
            {   //it's the close button
                EconomyUI.instance.PrintReceipt();
            }

        }

        //INITIALISATION
        private static void GetVanillaColumns(EconomyUI instance, Transform detailsUI)
        {   //gets the references to the vanilla columns and edits them if necessary
            
            islandNames = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textIslandNames").GetValue(instance);
            buyColumn = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textBuyPrice").GetValue(instance);
            goodName = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textGoodName").GetValue(instance);
            sellColumn = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textSellPrice").GetValue(instance);
            profitColumn = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textProfit").GetValue(instance);
            daysAgo = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textDaysAgo").GetValue(instance);
            percentText = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textProfitPercent").GetValue(instance);
            conversionFees = (TextMesh)AccessTools.Field(typeof(EconomyUI), "textConversionInfo").GetValue(instance);

            islandNames.characterSize = charSize;
            buyColumn.characterSize = charSize;
            sellColumn.characterSize = charSize;
            profitColumn.characterSize = charSize;
            daysAgo.characterSize = charSize;
            percentText.characterSize = charSize;
            conversionFees.characterSize = charSize;

            // Col 0 anchors on daysAgo's native position (adapts to any beta prefab change).
            // Col 6 anchors on a chosen right-page boundary to fill the visual space.
            // Pushing rightBound more positive pulls the grid to the left, away from the right edge.
            // Note: textProfitPercent in the beta is placed far off-screen by default, so we
            // deliberately do NOT use its native position to drive the step calculation.
            const float rightBound = -0.65f;                         // pulled left away from right margin
            float leftX = daysAgo.transform.localPosition.x;         // col 0 — adapt to native beta position
            float step  = (rightBound - leftX) / 6f;                 // 6 equal intervals for 7 columns (step is negative: X decreases rightward)

            // Store all 7 column positions
            colX[0] = leftX;                // days ago
            colX[1] = leftX + step * 1f;   // P. (production) — mod column
            colX[2] = leftX + step * 2f;   // buy
            colX[3] = leftX + step * 3f;   // sell
            colX[4] = leftX + step * 4f;   // profit
            colX[5] = leftX + step * 5f;   // %
            colX[6] = rightBound;           // p. pound (== leftX + step * 6f)

            float midX = (colX[0] + colX[6]) / 2f;

            // Force all columns to be UpperCenter aligned. The vanilla columns had mixed alignments 
            // (e.g. right-aligned for numbers, left-aligned for %), which caused them to visually
            // offset from our colX midlines and bleed into the pipes.
            daysAgo.anchor = TextAnchor.UpperCenter;
            daysAgo.alignment = TextAlignment.Center;
            buyColumn.anchor = TextAnchor.UpperCenter;
            buyColumn.alignment = TextAlignment.Center;
            sellColumn.anchor = TextAnchor.UpperCenter;
            sellColumn.alignment = TextAlignment.Center;
            profitColumn.anchor = TextAnchor.UpperCenter;
            profitColumn.alignment = TextAlignment.Center;
            percentText.anchor = TextAnchor.UpperCenter;
            percentText.alignment = TextAlignment.Center;

            // Move all columns to computed positions (textProfitPercent moved to colX[5] from its off-screen native position)
            Move(daysAgo.transform,      colX[0]);
            Move(buyColumn.transform,    colX[2]);
            Move(sellColumn.transform,   colX[3]);
            Move(profitColumn.transform, colX[4]);
            Move(percentText.transform,  colX[5]);

            Move(islandNames.transform, 0.23f);
            Move(conversionFees.transform, 0.64f, -0.8f);




            // Locate native grid elements
            List<TextMesh> nativePipes = new List<TextMesh>();
            TextMesh horizontalLine = null;

            foreach (Component comp in detailsUI.GetComponentsInChildren<Component>(true))
            {
                if (comp.GetType().Name.Contains("Text"))
                {
                    PropertyInfo prop = comp.GetType().GetProperty("text");
                    if (prop != null)
                    {
                        string text = prop.GetValue(comp, null) as string;
                        if (!string.IsNullOrEmpty(text))
                        {
                            string lowerText = text.ToLower();
                            if (lowerText.Contains("days ago") && lowerText.Contains("profit") && comp.name != "mod_header")
                            {
                                TextMesh header = (TextMesh)comp;
                                header.text = ""; // clear monolithic text
                                
                                string[] headers = { "days ago", "P.", "buy", "sell", "profit", "%", "p. pound" };
                                for (int i = 0; i < 7; i++)
                                {
                                    TextMesh h = Object.Instantiate(header, header.transform.parent);
                                    h.name = "mod_header_" + i;
                                    h.text = headers[i];
                                    h.characterSize = 0.85f;
                                    h.anchor = TextAnchor.UpperCenter;
                                    Move(h.transform, colX[i]); // Align perfectly with data column
                                }
                            }
                            else if (lowerText.Contains("|"))
                            {
                                string pure = lowerText.Replace("|", "").Replace(" ", "").Replace("\n", "").Replace("\r", "").Trim();
                                if (pure.Length == 0) 
                                {
                                    nativePipes.Add((TextMesh)comp);
                                }
                            }
                            else if (lowerText.Contains("___") || lowerText.Contains("---") || (text.Replace("_", "").Replace("-", "").Length < 5 && text.Length > 20))
                            {
                                horizontalLine = (TextMesh)comp;
                                horizontalLine.anchor = TextAnchor.UpperCenter;
                                Move(horizontalLine.transform, midX);
                                horizontalLine.text = "------------------------------------------------------------------------------------------------";
                            }
                        }
                    }
                }
            }

            // Fix the pipe grid
            // Hide the native standalone pipe lines to prevent rogue vertical lines
            foreach (TextMesh t in nativePipes)
            {
                t.gameObject.SetActive(false); 
            }

            // Create solid vertical lines
            TextMesh pipeTemplate = Object.Instantiate(daysAgo, detailsUI);
            pipeTemplate.name = "mod_pipeTemplate";
            
            // We use a long string of pipes and squeeze them together vertically to form a solid line
            pipeTemplate.text = "|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|\n|";
            pipeTemplate.lineSpacing = 0.5f; 
            pipeTemplate.anchor = TextAnchor.UpperCenter;

            List<TextMesh> cleanPipes = new List<TextMesh>();
            for (int p = 0; p < 6; p++)
            {
                TextMesh cp = Object.Instantiate(pipeTemplate, detailsUI);
                cp.name = "mod_clonedPipe_" + p;
                cleanPipes.Add(cp);
            }
            pipeTemplate.gameObject.SetActive(false);

            if (cleanPipes.Count >= 6)
            {
                Move(cleanPipes[0].transform, (colX[0] + colX[1]) / 2f);
                Move(cleanPipes[1].transform, (colX[1] + colX[2]) / 2f);
                Move(cleanPipes[2].transform, (colX[2] + colX[3]) / 2f);
                Move(cleanPipes[3].transform, (colX[3] + colX[4]) / 2f);
                Move(cleanPipes[4].transform, (colX[4] + colX[5]) / 2f);
                Move(cleanPipes[5].transform, (colX[5] + colX[6]) / 2f);
            }
        }
        private static void AddModColumns(EconomyUI instance, Transform detailsUI)
        {   //create the additional columns for the mod
            // colX[0-6] were computed and native columns moved in GetVanillaColumns.
            // This method only creates mod-specific columns and layout elements.

            float midX = (colX[0] + colX[6]) / 2f;  // true centre of the full 7-column span

            // Production column (P.) — mod-created, was missing from the current broken code
            if (productionText == null) productionText = Object.Instantiate(buyColumn, detailsUI);
            productionText.name = "productionText";
            productionText.characterSize = charSize;
            productionText.anchor = TextAnchor.UpperCenter;
            productionText.alignment = TextAlignment.Center;
            Move(productionText.transform, colX[1]);

            // percentText is the native beta textProfitPercent.
            // It is completely configured and positioned in GetVanillaColumns.

            // p. pound column — mod-created
            if (perPoundText == null) perPoundText = Object.Instantiate(buyColumn, detailsUI);
            perPoundText.name = "perPoundText";
            perPoundText.characterSize = charSize;
            perPoundText.anchor = TextAnchor.UpperCenter;
            perPoundText.alignment = TextAlignment.Center;
            Move(perPoundText.transform, colX[6]);

            //best deals sections - placed on the left page margin
            if (bdBestDeals == null) bdBestDeals = Object.Instantiate(buyColumn, detailsUI);
            bdBestDeals.name = "bdBestDeals";
            bdBestDeals.characterSize = 1f;
            bdBestDeals.text = "<color=#4D0000>★ Best Deals! ★</color>";
            bdBestDeals.transform.localRotation = Quaternion.identity; // Reset first
            bdBestDeals.transform.Rotate(0f, 180f, 15f); // Apply correct native flip + tilt
            Move(bdBestDeals.transform, 0.74f, -0.15f);
            bdBestDeals.anchor = TextAnchor.MiddleLeft;
            bdBestDeals.alignment = TextAlignment.Left;

            if (bdPercent == null) bdPercent = Object.Instantiate(buyColumn, detailsUI);
            bdPercent.name = "bdPercent";
            bdPercent.characterSize = 0.7f;
            bdPercent.text = "";
            Move(bdPercent.transform, 0.64f, -0.20f);
            bdPercent.anchor = TextAnchor.MiddleLeft;
            bdPercent.alignment = TextAlignment.Left;

            if (bdPerPound == null) bdPerPound = Object.Instantiate(buyColumn, detailsUI);
            bdPerPound.name = "bdPerPound";
            bdPerPound.characterSize = 0.7f;
            bdPerPound.text = "";
            Move(bdPerPound.transform, 0.64f, -0.25f);
            bdPerPound.anchor = TextAnchor.MiddleLeft;
            bdPerPound.alignment = TextAlignment.Left;

            if (bdAbsolute == null) bdAbsolute = Object.Instantiate(buyColumn, detailsUI);
            bdAbsolute.name = "bdAbsolute";
            bdAbsolute.characterSize = 0.7f;
            bdAbsolute.text = "";
            Move(bdAbsolute.transform, 0.64f, -0.30f);
            bdAbsolute.anchor = TextAnchor.MiddleLeft;
            bdAbsolute.alignment = TextAlignment.Left;

            //restore original text highlight bar
            if (highlightBar == null) highlightBar = Object.Instantiate(buyColumn, detailsUI).transform;
            highlightBar.name = "highlightBar";
            TextMesh hbText = highlightBar.GetComponent<TextMesh>();
            hbText.characterSize = 1f;
            hbText.text = "┌───────────────────────────────────┐\n└───────────────────────────────────┘";
            hbText.lineSpacing = 0.9f;
            hbText.color = new Color(0.25f, 0f, 0f);
            Move(highlightBar.transform, midX, 0.622f);
            hbText.anchor = TextAnchor.MiddleCenter;
        }

        private static void DisableUnusedUI(Transform detailsUI)
        {   //disables the vanilla highlight bar since it's not used
            Transform nativeHighlight = detailsUI.Find("highlight (parent)");
            if (nativeHighlight != null) nativeHighlight.gameObject.SetActive(false);
        }
        private static void InitializeUI()
        {   //Initializes the columns we'll edit and the disables the vanilla highlight bar
            islandNames.text = "";
            productionText.text = "";
            profitColumn.text = "";
            perPoundText.text = "";
            percentText.text = "";

            highlightBar.gameObject.SetActive(false);
        }
        private static void InitializeProd()
        {   //Initializes the portProd dictionary (so we don't have to run GetComponent every time)
            portProd.Clear();
            Port[] ports = Object.FindObjectsOfType<Port>();
            foreach (Port port in ports)
            {
                if (port.GetComponent<IslandMarket>() == null) continue;
                portProd[port.portIndex] = port.island.GetComponent<IslandMarket>().production;
            }
        }
        private static void InitializeGoods()
        {   //initializes the goods, goodNames and goodWeights arrays
            if (portProd.Count == 0) return;

            int length = 0;
            foreach (var p in portProd.Values) { length = p.Length; break; }

            goods = new Good[length];
            goodNames = new string[length];
            goodWeights = new float[length];
            for (int i = 0; i < length; i++)
            {
                ShipItem shipItem = PrefabsDirectory.instance.GetGood(i);
                if (shipItem == null) continue;

                goods[i] = shipItem.GetComponent<Good>();
                goodNames[i] = shipItem.name;
                goodWeights[i] = goods[i] != null ? goods[i].GetCargoWeight() : 0f;
            }
        }

        //METHODS
        private static void SetHighlightBar(int i)
        {   //moves the highlight bar to the correct island
            highlightBar.gameObject.SetActive(true);
            float hby = 0.622f - spacing * i;
            Move(highlightBar.transform, highlightBar.transform.localPosition.x, hby);
        }
        private static void GetProduction(int portIndex, int goodIndex)
        {   //Gets the production status of the good in all ports
            //Useful symbols: ✓ ↗ ↘ ✗ ★ ‼
            //productionText.text += $"{portProd[portIndex][goodIndex]}"; //for debugging

            if (!portProd.ContainsKey(portIndex) || goodIndex < 0 || goodIndex >= portProd[portIndex].Length)
            {
                productionText.text += "?\n";
                return;
            }

            if (portProd[portIndex][goodIndex] >= 8f)
            {   //great production
                productionText.text += $"★\n";
            }
            else if (portProd[portIndex][goodIndex] > 0f)
            {   //production
                productionText.text += $"✓\n";
            }
            else if (portProd[portIndex][goodIndex] <= -5f)
            {   //great consumption
                productionText.text += $"‼\n";
            }
            else if (portProd[portIndex][goodIndex] <= 0f)
            {   //no production
                productionText.text += $"✗\n";
            }
            else
            {   //edge case (something wrong)
                productionText.text += "error\n";
            }
        }
        private static int BuyP(int portIndex, int goodIndex)
        {   //Gets the buy price of the good
            object[] buypParameters = new object[] { portIndex, goodIndex };

            return (int)buypInfo.Invoke(EconomyUI.instance, buypParameters);
        }
        private static int SellP(int portIndex, int goodIndex)
        {   //Gets the sell price of the good
            object[] sellpParameters = new object[] { portIndex, goodIndex };
            
            return (int)sellpInfo.Invoke(EconomyUI.instance, sellpParameters);
        }
        private static void FindBestDeals(int[][] bookmark, int currentBookmark, IslandMarket currentMarket)
        {   //Finds the best deals for the best deals section
            #region Variables
            //max values
            int maxProfit = int.MinValue;
            float maxPercent = float.MinValue;
            float maxPerPound = float.MinValue;
            //max values good indexes
            int goodProfit = -1;
            int goodPercent = -1;
            int goodPerPound = -1;
            //max values port indexes
            int portProfit = -1;
            int portPercent = -1;
            int portPerPound = -1;
            #endregion

            int currentIsland = currentMarket.GetPortIndex();
            //Iterate through all goods
            for (int i = 0; i < goods.Length; i++)
            {   //iterate through all goods
                if (goods[i] == null) continue;
                
                int buyp = BuyP(currentIsland, i);  //buy price for i-esime good in the current island
                for (int j = 0; j < bookmark[currentBookmark].Length; j++)
                {   //iterate through all ports
                    int portIndex = bookmark[currentBookmark][j];
                    if (portIndex < 0 || portIndex >= currentMarket.knownPrices.Length || currentMarket.knownPrices[portIndex] == null || currentMarket.knownPrices[portIndex].buyPrices == null)
                    {
                        continue;
                    }
                    int sellp = SellP(portIndex, i);    //sell price for the i-esime good in the j-esime port
                    
                    int profit = buyp != 0 ? sellp - buyp : int.MinValue;
                    float profitPercent = buyp != 0 ? Mathf.Round(((float)profit / buyp) * 100f) : float.MinValue;
                    float cargoWeight = goodWeights[i];
                    float profitPerPound = buyp != 0 ? (float)Math.Round(profit / cargoWeight, 2) : float.MinValue;

                    //get max values
                    if (profit > maxProfit)
                    {
                        maxProfit = profit;
                        goodProfit = i;
                        portProfit = portIndex;
                    }
                    if (profitPercent > maxPercent)
                    {
                        maxPercent = profitPercent;
                        goodPercent = i;
                        portPercent = portIndex;
                    }
                    if (profitPerPound > maxPerPound)
                    {
                        maxPerPound = profitPerPound;
                        goodPerPound = i;
                        portPerPound = portIndex;
                    }
                }
            }
            //Write the best deals
            if (goodPercent != -1 && portPercent != -1 && goodNames[goodPercent] != null)
            {
                bdPercent.text = $"• {Capitalize(goodNames[goodPercent])} to {Port.ports[portPercent].GetPortName()} will return a profit of {maxPercent}%!";
            }
            else
            {
                bdPercent.text = "• No known deals for % profit";
            }

            if (goodPerPound != -1 && portPerPound != -1 && goodNames[goodPerPound] != null)
            {
                bdPerPound.text = $"• {Capitalize(goodNames[goodPerPound])} to {Port.ports[portPerPound].GetPortName()} will return a profit of {maxPerPound} per pound!";
            }
            else
            {
                bdPerPound.text = "• No known deals for per pound profit";
            }

            if (goodProfit != -1 && portProfit != -1 && goodNames[goodProfit] != null)
            {
                bdAbsolute.text = $"• {Capitalize(goodNames[goodProfit])} to {Port.ports[portProfit].GetPortName()} will return a profit of {maxProfit} per unit!";
            }
            else
            {
                bdAbsolute.text = "• No known deals for absolute profit";
            }
        }

        //HELPER METHODS
        private static void Move(Transform transform, float x)
        {   //Moves the given transform to the new x value
            Vector3 v = transform.localPosition;
            v.x = x;
            transform.localPosition = v;
        }
        private static void Move(Transform transform, float x, float y)
        {   //Moves the given transform to the new x and y values
            Vector3 v = transform.localPosition;
            v.x = x;
            v.y = y;
            transform.localPosition = v;
        }
        private static string Capitalize(string s)
        {   //capitalizes the first letter of a string
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpper(s[0]) + s.Substring(1);
        }
    }
}
