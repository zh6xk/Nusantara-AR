#import <Foundation/Foundation.h>
#import <UIKit/UIKit.h>
#import <AVFoundation/AVFoundation.h>

extern UIViewController* UnityGetGLViewController(void);

extern "C" {

    void Nusantara_SetTorch(bool enabled)
    {
        AVCaptureDevice *device = [AVCaptureDevice defaultDeviceWithMediaType:AVMediaTypeVideo];
        if (device != nil && [device hasTorch] && [device isTorchAvailable])
        {
            NSError *error = nil;
            if ([device lockForConfiguration:&error])
            {
                if (enabled && [device isTorchModeSupported:AVCaptureTorchModeOn])
                {
                    [device setTorchMode:AVCaptureTorchModeOn];
                }
                else if (!enabled && [device isTorchModeSupported:AVCaptureTorchModeOff])
                {
                    [device setTorchMode:AVCaptureTorchModeOff];
                }
                [device unlockForConfiguration];
            }
        }
    }

    void Nusantara_SaveImageToGallery(const char* filePath)
    {
        if (filePath == NULL) return;
        NSString *path = [NSString stringWithUTF8String:filePath];
        UIImage *image = [UIImage imageWithContentsOfFile:path];
        if (image != nil)
        {
            dispatch_async(dispatch_get_main_queue(), ^{
                UIImageWriteToSavedPhotosAlbum(image, nil, nil, nil);
            });
        }
    }

    void Nusantara_ShareImage(const char* filePath)
    {
        if (filePath == NULL) return;
        NSString *path = [NSString stringWithUTF8String:filePath];
        UIImage *image = [UIImage imageWithContentsOfFile:path];
        if (image == nil) return;

        dispatch_async(dispatch_get_main_queue(), ^{
            UIViewController *vc = UnityGetGLViewController();
            if (vc == nil) return;

            UIActivityViewController *activityVC = [[UIActivityViewController alloc] initWithActivityItems:@[image] applicationActivities:nil];
            if ([activityVC respondsToSelector:@selector(popoverPresentationController)] && activityVC.popoverPresentationController != nil)
            {
                activityVC.popoverPresentationController.sourceView = vc.view;
                activityVC.popoverPresentationController.sourceRect = CGRectMake(vc.view.bounds.size.width * 0.5, vc.view.bounds.size.height * 0.85, 1, 1);
            }
            [vc presentViewController:activityVC animated:YES completion:nil];
        });
    }

    void Nusantara_OpenPhotosApp(void)
    {
        dispatch_async(dispatch_get_main_queue(), ^{
            NSURL *url = [NSURL URLWithString:@"photos-redirect://"];
            if ([[UIApplication sharedApplication] canOpenURL:url])
            {
                [[UIApplication sharedApplication] openURL:url options:@{} completionHandler:nil];
            }
        });
    }
}
