import os
import subprocess

def refresh_disks():
    # Create a temporary script file for diskpart commands
    script_content = "rescan\n"
    script_path = os.path.join(os.getenv('TEMP'), 'diskpart_script.txt')

    with open(script_path, 'w') as script_file:
        script_file.write(script_content)

    # Run the diskpart command with the script
    subprocess.run(['diskpart', '/s', script_path], check=True)

    # Clean up the temporary script file
    os.remove(script_path)

if __name__ == "__main__":
    refresh_disks()
    print("Disk rescan completed.")
