﻿using System.Threading.Tasks;
using Microsoft.Azure.WebJobs;
using Newtonsoft.Json;
using Zavolokas.ImageProcessing.PatchMatch;
using Zavolokas.Structures;

namespace InpaintService.Activities
{
    public static class NnfRandomInitActivity
    {
        [FunctionName("RandomNnfInitIteration")]
        public static async Task NnfInit([ActivityTrigger] NnfInputData input)
        {
            var container = BlobHelper.OpenBlobContainer(input.Container);

            var imageBlob = container.GetBlockBlobReference(input.Image);
            var imageArgb = await BlobHelper.ConvertBlobToArgbImage(imageBlob);
            var image = imageArgb
                .FromArgbToRgb(new[] { 0.0, 0.0, 0.0 })
                .FromRgbToLab();

            var imageArea = Area2D.Create(0, 0, image.Width, image.Height);
            var pixelsArea = imageArea;

            var nnfSettings = input.Settings.PatchMatch;
            var calculator = input.IsCie79Calc
                ? ImagePatchDistance.Cie76
                : ImagePatchDistance.Cie2000;

            var nnfState = BlobHelper.ReadFromBlob<NnfState>(input.NnfName, container);
            var nnf = new Nnf(nnfState);

            var mappingState = BlobHelper.ReadFromBlob<Area2DMapState>(input.MappingNames[0], container);
            var mapping = new Area2DMap(mappingState);

            if (input.ExcludeInpaintArea)
            {
                var inpaintAreaState = BlobHelper.ReadFromBlob<Area2DState>(input.InpaintAreaName, container);
                var inpaintArea = Area2D.RestoreFrom(inpaintAreaState);
                pixelsArea = imageArea.Substract(inpaintArea);
            }

            var nnfBuilder = new PatchMatchNnfBuilder();
            nnfBuilder.RunRandomNnfInitIteration(nnf, image, image, nnfSettings, calculator, mapping, pixelsArea);

            var nnfData = JsonConvert.SerializeObject(nnf.GetState());
            BlobHelper.SaveJsonToBlob(nnfData, container, input.NnfName);
        }
    }
}