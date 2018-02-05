import xml.etree.ElementTree as ET
import xml.dom.minidom
import os

def update_xml(xmlin, xmlout):

    tree = ET.parse(xmlin)
    root = tree.getroot()    

    mat = root.find('Material')

    if mat == None:
        print(xmlin + ' skipped (no Material section)')
        return

    texpat = None

    for param in mat:
        if param.attrib['Name'] in ['DiffuseTexture', 'NormalTexture', 'Texture']:
            texpat = param.text
            break

    if texpat == None:
        print(xmlin + ' skipped (no base textures)')
        return

    generate = [('ColorMetalTexture', 'cm'), ('NormalGlossTexture', 'ng'), ('AddMapsTexture', 'add')]
    pattern = [x[0] for x in generate]
    exists = filter(lambda x: x.attrib['Name'] in pattern , mat)
    exists = map(lambda x: x.attrib['Name'], exists)
    
    path, ext = os.path.splitext(texpat)
    texname = ('_'.join(path.split('_')[:-1]))

    last = None
    for name, suf in generate:
        if name not in exists:
            p = ET.SubElement(mat, 'Parameter')
            p.attrib['Name'] = name
            p.text = '%s_%s.dds' % (texname, suf)
            p.tail = '\n\t\t'
            last = p
    
    if last != None:
        last.tail = '\n\t'

        xmlf = xml.dom.minidom.parseString(ET.tostring(root))
        xmlf.writexml(open(xmlout,'w'))
        print('"%s" processed and saved as "%s"' % (xmlin, xmlout))
        return
    print('"%s" skipped (textures already declared)' % xmlin)


for root, dirs, filenames in os.walk('.'):
    for f in filenames:
        
        x = os.path.join(root, f)
        if os.path.splitext(f)[1] == '.xml':
            update_xml(x, x)
