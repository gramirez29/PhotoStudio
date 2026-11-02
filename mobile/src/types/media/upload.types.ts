import type { GalleryResponse } from '../api/gallery';
import type { GalleryApi } from '../api/galleryApi';
import type { ImageSize, PickedFile } from '../utils/upload.types';

/** Where one photo of the queue is. */
export type UploadItemStatus = 'waiting' | 'preparing' | 'uploading' | 'registering' | 'failed';

/** One photo of the queue of an upload. */
export interface UploadItem {
  /** Number that tells the photo apart in the queue. */
  readonly id: number;
  /** The photo the photographer chose. */
  readonly file: PickedFile;
  /** Where the photo is. */
  readonly status: UploadItemStatus;
  /** Why it failed, in words, when `status` is `failed`; otherwise null. */
  readonly problem: string | null;
  /** How much of the photo has been sent, from 0 to 1; only meaningful while `status` is `uploading`. */
  readonly progress: number;
}

/** One final of the queue: the file and the photo of the selection it belongs to. */
export interface FinalUploadItem extends UploadItem {
  /** Identifier of the chosen photo the final belongs to. */
  readonly photoId: string;
}

/** What an uploader tells the screen while it works, so the screen can show it. */
export interface UploadObserver {
  /**
   * A photo changed where it is.
   * @param id The photo of the queue.
   * @param status Where it is now.
   * @param problem Why it failed, when it did.
   */
  onItem(id: number, status: UploadItemStatus, problem: string | null): void;

  /**
   * More of a photo reached the storage.
   * @param id The photo of the queue.
   * @param fraction How much was sent, from 0 to 1.
   */
  onProgress(id: number, fraction: number): void;

  /**
   * A photo was registered: it belongs to the gallery now and leaves the queue.
   * @param id The photo of the queue.
   */
  onDone(id: number): void;

  /**
   * A batch was registered.
   * @param gallery The gallery after it.
   */
  onRegistered(gallery: GalleryResponse): void;
}

/** How a run of an uploader ended. */
export interface UploadOutcome {
  /** True when a request failed and the rest of the photos were left waiting, to try again when the photographer decides. */
  readonly stopped: boolean;
  /** Why it stopped, in words; null when it did not. */
  readonly problem: string | null;
}

/** Where and how to send a file to the storage: a signed address and the headers that are part of its signature. */
export interface StorageTarget {
  /** Signed address to PUT the file to. It is a secret that expires: it is never logged or shown. */
  readonly url: string;
  /** Headers the upload must send exactly as given. */
  readonly headers: Readonly<Record<string, string>>;
}

/** Options of one upload to the storage. */
export interface StorageUploadOptions {
  /** Called as the file is sent, with the fraction sent from 0 to 1. */
  readonly onProgress?: ((fraction: number) => void) | undefined;
  /** Signal that cancels the upload. */
  readonly signal?: AbortSignal | undefined;
}

/** Sends files from the phone straight to the storage; tests give another one. */
export interface StorageUploader {
  /**
   * Sends a file with a PUT.
   * @param target The signed address and its headers.
   * @param uri Address of the file on the phone.
   * @param options Progress and cancellation.
   * @returns A promise that resolves when the storage has the file, and rejects when it did not accept it.
   */
  put(target: StorageTarget, uri: string, options?: StorageUploadOptions): Promise<void>;
}

/** A file prepared by the app that waits in the cache of the phone until it is sent. */
export interface StagedFile {
  /** Address of the file on the phone. */
  readonly uri: string;
  /** Size in bytes. */
  readonly bytes: number;
}

/** Keeps the files the app prepares until they are sent. */
export interface FileStaging {
  /**
   * Writes bytes to a new file in the cache.
   * @param bytes What to write.
   * @returns The file.
   */
  stage(bytes: Uint8Array): StagedFile;

  /**
   * Deletes a staged file. It never fails: a file that is already gone is not a problem.
   * @param uri Address of the file.
   */
  discard(uri: string): void;
}

/** Why a photo could not become a proof. */
export type ProofRenderFailure = 'decode' | 'encode' | 'too_big';

/** A photo opened by the engine of proofs, ready to be drawn at any size. */
export interface ProofSource {
  /** Width in pixels of the photo as the engine holds it, already turned the way the camera had it. */
  readonly width: number;
  /** Height in pixels of the photo as the engine holds it. */
  readonly height: number;

  /**
   * Draws the photo at a size with the watermark across the middle and encodes it as a JPEG.
   * @param size Size of the proof.
   * @param quality JPEG quality from 0 to 100.
   * @returns The bytes, or null when they could not be made.
   */
  encode(size: ImageSize, quality: number): Promise<Uint8Array | null>;

  /** Lets go of what the engine holds for this photo. */
  close(): void;
}

/** What the renderer of proofs needs from the device: open a photo, reduced, in a form that can be drawn on. */
export interface ProofEngine {
  /**
   * Opens a photo of the phone.
   * @param uri Address of the photo.
   * @returns The photo; rejects when it cannot be read.
   */
  open(uri: string): Promise<ProofSource>;
}

/** A proof that was made. */
export interface RenderedProof {
  /** The JPEG. */
  readonly bytes: Uint8Array;
  /** Size in pixels. */
  readonly size: ImageSize;
  /** Quality it was encoded with, from 0 to 100. */
  readonly quality: number;
}

/** Turns photos into proofs. */
export interface ProofRenderer {
  /**
   * Makes the proof of a photo.
   * @param uri Address of the photo on the phone.
   * @returns The proof; rejects with a `ProofRenderError` that says why it could not be made.
   */
  render(uri: string): Promise<RenderedProof>;
}

/** What the renderer of thumbnails needs from the device: reduce a photo to a size and quality and keep the result as a file. */
export interface ThumbEngine {
  /**
   * Makes a thumbnail. The engine knows the real size of the photo once it is opened (turned the way the camera had it), so it is
   * the one that works out the size from the longest side.
   * @param uri Address of the photo on the phone.
   * @param longSide Longest side of the thumbnail in pixels; a smaller photo is never enlarged.
   * @param quality JPEG quality from 0 to 1.
   * @returns The thumbnail as a staged file, or null when it could not be made.
   */
  make(uri: string, longSide: number, quality: number): Promise<StagedFile | null>;

  /**
   * Deletes a thumbnail that was made.
   * @param uri Address of the thumbnail.
   */
  discard(uri: string): void;
}

/** Makes the thumbnail of a final. */
export interface ThumbRenderer {
  /**
   * Makes the thumbnail of a final. It never fails: when it cannot be made it gives null and the final goes up without one.
   * @param file The final.
   * @returns The thumbnail, or null.
   */
  render(file: PickedFile): Promise<StagedFile | null>;
}

/** What the uploader of proofs needs from outside; tests give others. */
export interface ProofUploaderDeps {
  /** The two requests of the API that an upload of proofs uses. */
  readonly api: Pick<GalleryApi, 'requestProofUploads' | 'registerProofs'>;
  /** Turns photos into proofs. */
  readonly renderer: ProofRenderer;
  /** Keeps the proofs until they are sent. */
  readonly staging: FileStaging;
  /** Sends files to the storage. */
  readonly storage: StorageUploader;
}

/** What the uploader of finals needs from outside; tests give others. */
export interface FinalUploaderDeps {
  /** The two requests of the API that an upload of finals uses. */
  readonly api: Pick<GalleryApi, 'requestFinalUploads' | 'registerFinals'>;
  /** Makes the thumbnail of a final. */
  readonly thumbs: ThumbRenderer;
  /** Deletes the thumbnails once they are sent. */
  readonly staging: Pick<FileStaging, 'discard'>;
  /** Sends files to the storage. */
  readonly storage: StorageUploader;
}

/** What the uploader of the ZIP needs from outside; tests give others. */
export interface ZipUploaderDeps {
  /** The two requests of the API that an upload of the ZIP uses. */
  readonly api: Pick<GalleryApi, 'requestZipUpload' | 'registerZip'>;
  /** Sends files to the storage. */
  readonly storage: StorageUploader;
}

/** The uploader of proofs. */
export interface ProofUploader {
  /**
   * Uploads a list of photos to a gallery as proofs, a batch at a time: each batch is prepared, sent straight to the storage and
   * registered before the next starts. A photo that fails does not stop the others; a request that fails (the API, or no connection)
   * stops the run and leaves the rest waiting.
   * @param galleryId Gallery the proofs go to.
   * @param items The photos to upload.
   * @param observer Told about every change, so the screen can show it.
   * @param signal Optional signal to cancel the run (for example when the screen is left).
   * @returns How the run ended.
   */
  run(galleryId: string, items: readonly UploadItem[], observer: UploadObserver, signal?: AbortSignal): Promise<UploadOutcome>;
}

/** The uploader of final photos. */
export interface FinalUploader {
  /**
   * Uploads a list of finals to a gallery, a few at a time: the places are asked right before each batch, the files go straight to
   * the storage and the batch is registered before the next starts.
   * @param galleryId Gallery the finals belong to.
   * @param items The finals to upload.
   * @param observer Told about every change, so the screen can show it.
   * @param signal Optional signal to cancel the run.
   * @returns How the run ended.
   */
  run(galleryId: string, items: readonly FinalUploadItem[], observer: UploadObserver, signal?: AbortSignal): Promise<UploadOutcome>;
}

/** How the upload of the ZIP ended. */
export interface ZipUploadOutcome {
  /** The gallery with its new ZIP, or null when the upload did not finish. */
  readonly gallery: GalleryResponse | null;
  /** Why it did not finish, in words; null when it did or when it was cancelled. */
  readonly problem: string | null;
}

/** The uploader of the ZIP. */
export interface ZipUploader {
  /**
   * Uploads the ZIP of a gallery: asks for the place, sends the file straight to the storage with its progress, and registers it.
   * @param galleryId Gallery the ZIP belongs to.
   * @param file The ZIP.
   * @param onProgress Called as the file is sent, with the fraction from 0 to 1.
   * @param signal Optional signal to cancel the upload.
   * @returns How it ended.
   */
  run(galleryId: string, file: PickedFile, onProgress: (fraction: number) => void, signal?: AbortSignal): Promise<ZipUploadOutcome>;
}
