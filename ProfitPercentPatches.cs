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
        private static TextMesh productionText = new TextMesh();
        private static TextMesh percentText = new TextMesh();
        private static TextMesh perPoundText = new TextMesh();
        private static TextMesh highlightBar = new TextMesh();

        //Vanilla columns
        private static TextMesh islandNames;
        private static TextMesh buyColumn;
        private static TextMesh goodName;
        private static TextMesh sellColumn;
        private static TextMesh header;
        private static TextMesh profitColumn;
        private static TextMesh daysAgo;
        private static TextMesh tabLines;
        private static TextMesh horizontalLine;
        private static TextMesh conversionFees;

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

        //PATCHES
        public static void AwakePatch(EconomyUI __instance)
        {   //initialises the new UI at game start
            
            //Store MethodInfos
            buypInfo = AccessTools.Method(typeof(EconomyUI), "GetBuyPrice");
            sellpInfo = AccessTools.Method(typeof(EconomyUI), "GetSellPrice");

            Transform detailsUI = __instance.transform.Find("good details (right panel)").Find("details UI");

            //Assign vanilla columns references
            GetVanillaColumns(detailsUI);
            AddModColumns(detailsUI);
            DisableUnusedUI(detailsUI);

            //Cache port production values
            InitializeProd();
        }
        public static void MainPatch(int[][] ___bookmarkIslands, int ___currentBookmark, IslandMarket ___currentIsland)
        {   //Main patch for the trade UI - This is called every time the page is refreshed

            //Capitalise the good name
            goodName.text = Capitalize(goodName.text);

            //CREATE THE UI
            InitializeUI();
            if (goods == null) InitializeGoods();
            for (int i = 0; i < ___bookmarkIslands[___currentBookmark].Length; i++)
            {
                int portIndex = ___bookmarkIslands[___currentBookmark][i];
                int goodIndex = EconomyUI.instance.currentSelectedGood;

                if (portIndex == ___currentIsland.GetPortIndex())
                {
                    SetHighlightBar(i);
                }

                if (ProfitPercentMain.showBestDealsConfig.Value)
                {
                    bdBestDeals.text = "<color=#4D0000>★ Best Deals! ★</color>";
                    FindBestDeals(___bookmarkIslands, ___currentBookmark, ___currentIsland);
                }
                else
                {
                    bdBestDeals.text = "";
                    bdPercent.text = "";
                    bdPerPound.text = "";
                    bdAbsolute.text = "";
                }
                GetProduction(portIndex, goodIndex);
                if (portIndex < 0 || portIndex >= ___currentIsland.knownPrices.Length || ___currentIsland.knownPrices[portIndex] == null || ___currentIsland.knownPrices[portIndex].buyPrices == null)
                {
                    if (portIndex == ___currentIsland.GetPortIndex())
                    {   //current island
                        if (ProfitPercentMain.coloredTextConfig.Value)
                        {
                            islandNames.text += $"<color=#4D0000>• {Port.ports[portIndex].GetPortName()}</color>\n";
                        }
                        else
                        {
                            islandNames.text += $"• {Port.ports[portIndex].GetPortName()}\n";
                        }
                    }
                    else
                    {
                        islandNames.text += $"{Port.ports[portIndex].GetPortName()}\n";
                    }
                    profitColumn.text += $"?\n";
                    percentText.text += $"?\n";
                    perPoundText.text += $"?\n";
                }
                else
                {
                    int buyp = BuyP(___currentIsland.GetPortIndex(), goodIndex);
                    int sellp = SellP(portIndex, goodIndex);
                    int profit = sellp - buyp;

                    //Profit percent
                    float profitPercent = Mathf.Round(((float)profit / buyp) * 100f);

                    //Profit per pound
                    float cargoWeight = goodWeights[goodIndex];
                    float profitPerPound = (float)Math.Round(profit / cargoWeight, 2);

                    if (ProfitPercentMain.coloredTextConfig.Value)
                    {   //With colors
                        //Island names
                        if (portIndex == ___currentIsland.GetPortIndex())
                        {   //current island
                            islandNames.text += $"<color=#4D0000>• {Port.ports[portIndex].GetPortName()}</color>\n";
                        }
                        else
                        {   //not the currentIsland
                            islandNames.text += $"{Port.ports[portIndex].GetPortName()}\n";
                        }
                        int higherThreshold = ProfitPercentMain.blueThresholdConfig.Value;
                        int lowerThreshold = ProfitPercentMain.greenThresholdConfig.Value;
                        if (ProfitPercentMain.blueThresholdConfig.Value < ProfitPercentMain.greenThresholdConfig.Value)
                        {   //make sure the thresholds are used correctly by switching them around if necessary
                            higherThreshold = ProfitPercentMain.greenThresholdConfig.Value;
                            lowerThreshold = ProfitPercentMain.blueThresholdConfig.Value;
                        }
                        if (float.IsInfinity(profitPercent))
                        {   //if buyp is zero (good not sold in the current port), we get infinite profit. In that case we add a yellow -.
                            profitColumn.text += $"<color=#CC7F00>- </color>\n";
                            percentText.text += $"<color=#CC7F00>- </color>\n";
                            perPoundText.text += $"<color=#CC7F00>- </color>\n";
                        }
                        else if (profitPercent > higherThreshold)
                        {   //blue color #051139
                            profitColumn.text += $"<color=#051139>{profit}</color>\n";
                            percentText.text += $"<color=#051139>{profitPercent}<size=40%>%</size></color>\n";
                            perPoundText.text += $"<color=#051139>{profitPerPound}</color>\n";
                        }
                        else if (profitPercent > lowerThreshold)
                        {   //green color #003300 // old color was #113905
                            profitColumn.text += $"<color=#003300>{profit}</color>\n";
                            percentText.text += $"<color=#003300>{profitPercent}<size=40%>%</size></color>\n";
                            perPoundText.text += $"<color=#003300>{profitPerPound}</color>\n";
                        }
                        else if (profitPercent < 0f)
                        {   //red color #4D0000 //old color was #7C0000
                            profitColumn.text += $"<color=#4D0000>{profit}</color>\n";
                            percentText.text += $"<color=#4D0000>{profitPercent}<size=40%>%</size></color>\n";
                            perPoundText.text += $"<color=#4D0000>{profitPerPound}</color>\n";
                        }
                        else // between the green threshold and zero color is yellow
                        {   //yellow color #CC7F00
                            profitColumn.text += $"<color=#CC7F00>{profit}</color>\n";
                            percentText.text += $"<color=#CC7F00>{profitPercent}<size=40%>%</size></color>\n";
                            perPoundText.text += $"<color=#CC7F00>{profitPerPound}</color>\n";
                        }
                    }
                    else
                    {   //Without colors
                        //Island names
                        if (portIndex == ___currentIsland.GetPortIndex())
                        {   //current island
                            islandNames.text += $"• {Port.ports[portIndex].GetPortName()}\n";
                        }
                        else
                        {   //not the currentIsland
                            islandNames.text += $"{Port.ports[portIndex].GetPortName()}\n";
                        }
                        if (float.IsInfinity(profitPercent))
                        {   //if buyp is zero (good not sold in the current port), we get infinite profit. in that case we add a yellow -.
                            profitColumn.text += $"- \n";
                            percentText.text += $"- \n";
                            perPoundText.text += $"- \n";
                        }
                        else
                        {
                            profitColumn.text += $"{profit}\n";
                            percentText.text += $"{profitPercent}<size=40%>%</size>\n";
                            perPoundText.text += $"{profitPerPound}\n";
                        }
                    }
                }
            }
        }
        public static void ButtonPatch(EconomyUIButton __instance)
        {   //Automatically get the receipt when closing the trade UI
            
            if (__instance.name == "bookmark_button_X" && ProfitPercentMain.autoReceiptConfig.Value && EconomyUIReceiptScribe.instance.ReceiptAvailable())
            {   //it's the close button
                EconomyUI.instance.PrintReceipt();
            }

        }

        //INITIALISATION
        private static void GetVanillaColumns(Transform detailsUI)
        {   //gets the references to the vanilla columns and edits them if necessary
            
            islandNames = detailsUI.GetChild(2).GetComponent<TextMesh>();
            islandNames.characterSize = charSize;
            Move(islandNames.transform, 0.23f);

            buyColumn = detailsUI.GetChild(3).GetComponent<TextMesh>();
            buyColumn.characterSize = charSize;
            Move(buyColumn.transform, -0.11f);
            goodName = detailsUI.GetChild(6).GetComponent<TextMesh>();

            sellColumn = detailsUI.GetChild(7).GetComponent<TextMesh>();
            sellColumn.characterSize = charSize;
            Move(sellColumn.transform, -0.27f);

            //header
            header = detailsUI.GetChild(8).GetComponent<TextMesh>();
            header.characterSize = 0.85f;
            header.text = "days ago  P.    buy     sell     profit      %    p. pound";

            profitColumn = detailsUI.GetChild(9).GetComponent<TextMesh>();
            profitColumn.characterSize = charSize;
            Move(profitColumn.transform, -0.45f);

            daysAgo = detailsUI.GetChild(11).GetComponent<TextMesh>();
            daysAgo.characterSize = charSize;
            Move(daysAgo.transform, 0.133f);

            //table lines
            tabLines = detailsUI.GetChild(13).GetComponent<TextMesh>();
            tabLines.text = "        |   |       |       |        |       |";

            horizontalLine = detailsUI.GetChild(14).GetComponent<TextMesh>();
            Move(horizontalLine.transform, -0.94f);

            conversionFees = detailsUI.GetChild(16).GetComponent<TextMesh>();
            conversionFees.characterSize = charSize;
            Move(conversionFees.transform, 0.64f, -0.8f);
        }
        private static void AddModColumns(Transform detailsUI)
        {   //create the additional columns for the mod
            
            productionText = Object.Instantiate(buyColumn, detailsUI);
            productionText.name = "productionText";
            productionText.characterSize = charSize;
            Move(productionText.transform, 0.050f);

            percentText = Object.Instantiate(buyColumn, detailsUI);
            percentText.name = "percentText";
            percentText.characterSize = charSize;
            Move(percentText.transform, -0.61f);

            perPoundText = Object.Instantiate(buyColumn, detailsUI);
            perPoundText.name = "perPoundText";
            perPoundText.characterSize = charSize;
            Move(perPoundText.transform, -0.78f);

            //best deals sections
            bdBestDeals = Object.Instantiate(buyColumn, detailsUI);
            bdBestDeals.name = "bdBestDeals";
            bdBestDeals.characterSize = 1f;
            bdBestDeals.text = "<color=#4D0000>★ Best Deals! ★</color>";
            bdBestDeals.transform.Rotate(0f, 0f, 15f);
            Move(bdBestDeals.transform, 0.74f, -0.15f);
            bdBestDeals.anchor = TextAnchor.MiddleLeft;


            bdPercent = Object.Instantiate(buyColumn, detailsUI);
            bdPercent.name = "bdPercent";
            bdPercent.characterSize = 0.7f;
            bdPercent.text = "Salmon from here to there will give X in profit";
            Move(bdPercent.transform, 0.64f, -0.20f);
            bdPercent.anchor = TextAnchor.MiddleLeft;

            bdPerPound = Object.Instantiate(buyColumn, detailsUI);
            bdPerPound.name = "bdPerPound";
            bdPerPound.characterSize = 0.7f;
            bdPerPound.text = "Salmon from here to there will give X in profit";
            Move(bdPerPound.transform, 0.64f, -0.25f);
            bdPerPound.anchor = TextAnchor.MiddleLeft;

            bdAbsolute = Object.Instantiate(buyColumn, detailsUI);
            bdAbsolute.name = "bdAbsolute";
            bdAbsolute.characterSize = 0.7f;
            bdAbsolute.text = "Salmon from here to there will give X in profit";
            Move(bdAbsolute.transform, 0.64f, -0.30f);
            bdAbsolute.anchor = TextAnchor.MiddleLeft;

            //highlight bar
            highlightBar = Object.Instantiate(buyColumn, detailsUI);
            highlightBar.name = "highlightBar";
            highlightBar.characterSize = 1f;
            highlightBar.text = "┌───────────────────────────────────┐\n└───────────────────────────────────┘";
            highlightBar.lineSpacing = 0.9f;
            highlightBar.color = new Color(0.25f, 0f, 0f);
            Move(highlightBar.transform, 0.64f, 0.622f);
            highlightBar.anchor = TextAnchor.MiddleLeft;
        }
        private static void DisableUnusedUI(Transform detailsUI)
        {   //disables the vanilla highlight bar since it's not used
            detailsUI.Find("highlight (parent)").gameObject.SetActive(false);
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
