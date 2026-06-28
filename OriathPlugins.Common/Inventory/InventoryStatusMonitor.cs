namespace OriathPlugins.Common.Inventory;



using OriathHub.RemoteEnums;

using OriathHub.RemoteObjects.States.InGameStateObjects;



public sealed class InventoryStatus

{

    public int UsedSlots;



    public int TotalSlots;



    public int FreeSlots;



    public double DivineCount;



    public double ChaosCount;



    public bool HasMainInventory;

}



public static class InventoryStatusMonitor

{

    private const string DivinePathMarker = "CurrencyModValues";

    private const string ChaosPathMarker = "CurrencyRerollRare";



    public static InventoryStatus Read(ServerData? serverData)

    {

        var status = new InventoryStatus();

        if (serverData is null)

        {

            return status;

        }



        var inventory = serverData.GetInventory(InventoryName.MainInventory1);

        if (inventory.TotalBoxes.X > 0 && inventory.TotalBoxes.Y > 0)

        {

            status.HasMainInventory = true;

            status.TotalSlots = inventory.TotalBoxes.X * inventory.TotalBoxes.Y;

            for (var y = 0; y < inventory.TotalBoxes.Y; y++)

            {

                for (var x = 0; x < inventory.TotalBoxes.X; x++)

                {

                    if (inventory[y, x].IsValid)

                    {

                        status.UsedSlots++;

                    }

                }

            }

        }



        status.FreeSlots = Math.Max(0, status.TotalSlots - status.UsedSlots);

        var currencySnapshot = InventoryScanner.BuildCurrencySnapshot(serverData, debugLogging: false);

        foreach (var (path, quantity) in currencySnapshot)

        {

            if (path.Contains(DivinePathMarker, StringComparison.OrdinalIgnoreCase))

            {

                status.DivineCount += quantity;

            }



            if (path.Contains(ChaosPathMarker, StringComparison.OrdinalIgnoreCase) &&

                !path.Contains("CurrencyRerollRare2", StringComparison.OrdinalIgnoreCase) &&

                !path.Contains("CurrencyRerollRare3", StringComparison.OrdinalIgnoreCase))

            {

                status.ChaosCount += quantity;

            }

        }



        return status;

    }

}


