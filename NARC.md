# NARC
_Nitro Archive_: File documentation.  
  
NARC files are commonly used container files in first party and co-developed Nintendo games.  
  
They are meant to be used with the Nintendo DS, though they also appear in select games for systems like the Nintendo 3DS, Wii U or Nintendo Switch.

## Header
| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x04   | char[4] | Magic value in ASCII: "NARC"                  |
| 0x04   | 0x02   | uint16  | Endianess: 0xFEFF Little, 0xFFFE Big          |
| 0x06   | 0x02   | uint16  | Version                                       |
| 0x08   | 0x04   | uint32  | Total file length                             |
| 0x0C   | 0x02   | uint16  | Header length (constant 16)                   |
| 0x0E   | 0x02   | uint16  | Section count (constant 3)                    |

After the header, 3 sections are stored (**FATB**, **FNTB** and **FIMG**).

## FATB section

Used to store file offsets.

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x04   | char[4] | Magic value in ASCII: "BTAF"                  |
| 0x04   | 0x04   | uint32  | Section length                                |
| 0x08   | 0x04   | uint32  | File count                                    |
| 0x0C   | ...    |         | Offsets                                       |

### FATB file entry

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x04   | uint32  | Offset to file's beginning                    |
| 0x04   | 0x04   | uint32  | Offset to file's end                          |

The offsets are relative to the position after the FIMG section's length.

## FNTB section

Note: Sometimes the FNTB section will be empty, which means that the NARC contains is nameless.

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x04   | char[4] | Magic value in ASCII: "BTNF"                  |
| 0x04   | 0x04   | uint32  | Section length                                |
| 0x08   | ...    |         | Directory entries                             |
|        | ...    |         | File and directory names                      |

### FNTB directory entry

Each directory, including the root, has a directory entry to identify it. They are stored in an array, one after the other:

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x04   | uint32  | Offset to the directory's start position      |
| 0x04   | 0x04   | uint16  | Amount of files present before this directory |
| 0x08   | 0x04   | uint16  | Children directories count (0xF0 if none)     |

Note: The offsets are relative to the beginning of the directory entries array.

### FNTB name entries

Name entries are stored as length-prefixed strings.  
The actual lenght is stored in the 7 less significant bits while the most significant one is reserved to identify whether the entry is a file (0) or a directory (1).

#### Directories

The directory's ID and constant byte 0xF0 are written right after the name.

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x01   | uint8   | 0x80 & Name's length                          |
| 0x01   | ...    | char[]  | Directory's name                              |
| ...    | 0x01   | uint8   | Directory's ID                                | 
| ...    | 0x01   | uint8   | Constant byte (0xF0)                          |

Note: Directory IDs always start with 1. The root directory might be 0.

#### Files

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x01   | uint8   | Name's length                                 |
| 0x01   | ...    | char[]  | File's name                                   |

#### Directory ends

Directory ends are marked with a null byte (0).  
Therefore, whenever a length of 0 is found, it means to go up one directory.

## FIMG section

The FIMG section holds all the file contents inside it.

| Offset | Length | Type    | Description                                   |
|--------|--------|---------|-----------------------------------------------|
| 0x00   | 0x04   | char[4] | Magic value in ASCII: "GMIF"                  |
| 0x04   | 0x04   | uint32  | Section length                                |
| 0x08   | ...    |         | File data                                     |

### Alignment

Depending on the game an alignment of the file data and the FMIG section itself will need to be performed in order for the game to read the file correctly. Normally, this alignment is of 128 bytes.  
  
Note that games like modern Mario Tennis, which use a special compression within the file itself, require an alignment to 16 bytes and some other games use no alignment at all.
