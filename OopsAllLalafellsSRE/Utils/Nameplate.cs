using Dalamud.Game.Gui.NamePlate;
using FFXIVClientStructs.FFXIV.Client.Game.Object;

namespace OopsAllLalafellsSRE.Utils
{
    internal class Nameplate
    {
        public Nameplate()
        {
            Service.namePlateGui.OnNamePlateUpdate += (context, handlers) =>
            {
                if (!Service.configuration.enabled)
                    return;

                foreach (var handler in handlers)
                {
                    if (handler.NamePlateKind == NamePlateKind.PlayerCharacter)
                    {
                        unsafe
                        {
                            nint? gameObjPtr = handler.GameObject?.Address;
                            if (gameObjPtr == null)
                                return;

                            var gameObj = (GameObject*)gameObjPtr;
                            if (Service.configuration.nameHQ && !Drawer.NonNativeID.Contains(gameObj->NameString))
                            {
                                handler.NameParts.Text = $"{gameObj->NameString} \uE03C";
                            }
                        }
                    }
                }
            };
        }

        public void Dispose() { }
    }
}
