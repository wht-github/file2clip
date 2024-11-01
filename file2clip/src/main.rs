use std::ffi::CString;
use std::ptr;
use windows::core::Result;
use windows::Win32::Foundation::{BOOL, HANDLE, HWND, POINT};
use windows::Win32::System::DataExchange::{
    CloseClipboard, EmptyClipboard, OpenClipboard, SetClipboardData,
};
use windows::Win32::System::Memory::{GlobalAlloc, GlobalLock, GlobalUnlock, GMEM_MOVEABLE};
use windows::Win32::System::Ole::CF_HDROP;
use windows::Win32::UI::Shell::DROPFILES;
fn main() {
    let args: Vec<String> = std::env::args().collect();
    if args.len() < 2 {
        println!("Please provide file paths as arguments.");
        return;
    }

    let files: Vec<String> = args[1..].to_vec();
    for file in &files {
        if !std::path::Path::new(file).exists() {
            println!("File not found: {}", file);
            return;
        } else {
            println!("File found: {}", file);
        }
    }
    let files: Vec<String> = files
        .iter()
        .map(|file| {
            std::path::absolute(file)
                .unwrap()
                .to_string_lossy()
                .to_string()
        })
        .collect();

    println!("Files: {:?}", files);

    set_file_drop(&files);
}

fn set_file_drop(files: &[String]) {
    unsafe {
        let mut size = 0;
        for file in files {
            let tmp_chars = CString::new(file.as_str()).unwrap();
            size += (tmp_chars.count_bytes() + 1) * std::mem::size_of::<u16>();
        }
        size += std::mem::size_of::<DROPFILES>() + std::mem::size_of::<u16>();
        match GlobalAlloc(GMEM_MOVEABLE, size) {
            Ok(h_global) => {
                let drop_files = DROPFILES {
                    pFiles: std::mem::size_of::<DROPFILES>() as u32,
                    pt: POINT { x: 0, y: 0 },
                    fNC: BOOL(0),
                    fWide: BOOL(1),
                };

                let p_global = GlobalLock(h_global);
                std::ptr::copy_nonoverlapping(
                    &drop_files as *const _ as *const u8,
                    p_global as *mut u8,
                    std::mem::size_of::<DROPFILES>(),
                );

                let mut file_path_ptr = p_global.add(std::mem::size_of::<DROPFILES>());
                for file in files {
                    let u16_chars = file.encode_utf16().collect::<Vec<u16>>();
                    // for (i, &byte) in  u16_chars.iter().enumerate() {
                    //     *(file_path_ptr.add(i * 2) as *mut u16) = byte as u16;
                    // }
                    // file_path_ptr = file_path_ptr.add(u16_chars.len() * 2);

                    std::ptr::copy_nonoverlapping(
                        u16_chars.as_ptr(),
                        file_path_ptr as *mut u16,
                        u16_chars.len(),
                    );
                    file_path_ptr = file_path_ptr.add(u16_chars.len() * 2);
                    *(file_path_ptr as *mut u16) = 0;
                    file_path_ptr = file_path_ptr.add(std::mem::size_of::<u16>());
                }
                *(file_path_ptr as *mut u16) = 0;

                _ = GlobalUnlock(h_global);
                match OpenClipboard(HWND(ptr::null_mut())) {
                    Ok(_) => match EmptyClipboard() {
                        Ok(_) => {
                            println!("Clipboard cleared.");
                            match SetClipboardData(15 as u32, HANDLE(h_global.0)) {
                                Ok(_) => {
                                    println!("Files copied to clipboard.");
                                }
                                Err(e) => {
                                    println!("Failed to set clipboard data: {:?}", e);
                                    return;
                                }
                            }
                            CloseClipboard().unwrap();
                        }
                        Err(e) => {
                            println!("Failed to clear clipboard: {:?}", e);
                        }
                    },

                    Err(e) => {
                        println!("Failed to open clipboard: {:?}", e);
                        return;
                    }
                }
            }
            Err(e) => {
                println!("Failed to allocate memory: {:?}", e);
                return;
            }
        };
    }
}
