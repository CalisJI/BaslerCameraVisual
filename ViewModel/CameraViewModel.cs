using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Basler.Pylon;

namespace BaslerCameraVisual.ViewModel
{
    public class CameraViewModel : BaseViewModel.BaseViewModel
    {
        public CameraViewModel()
        {

        }

        private void Opencam() 
        {
            using(Camera camera= new Camera())
            {
            
            }
        }
    }
}
