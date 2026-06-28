namespace OriathPlugins.Common.Inventory;



using OriathHub.RemoteObjects.Components;

using OriathHub.RemoteObjects.States.InGameStateObjects;



public static class ItemStackCountReader

{

    public static int Read(Item item) => ReadStack(item);



    public static int Read(Entity entity) => ReadStack(entity);



    private static int ReadStack(Entity entity)

    {

        if (!entity.IsValid)

        {

            return 1;

        }



        return entity.TryGetComponent<Stack>(out var stack) && stack.Count > 0 ? stack.Count : 1;

    }

}


