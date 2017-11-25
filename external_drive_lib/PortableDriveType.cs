using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace external_drive_lib
{
    public enum PortableDriveType
    {
        Portable,
        // if this, we're not sure if it's phone or tablet or whatever
        AndroidUnknown,
        // it's an android phone
        AndroidPhone,
        // it's an android tablet
        AndroidTablet,

        IosUnknown,
        Iphone,
        Ipad,

        // SD Card
        // FIXME can i know if it's read-only?
        SdCard,
        // external hard drive
        // FIXME can i know if it's read-only?
        ExternalHdd,

        // it's the Windows HDD 
        InternalHdd,

        // FIXME this is to be treated read-only!!!
        CdRom,
    }
}
